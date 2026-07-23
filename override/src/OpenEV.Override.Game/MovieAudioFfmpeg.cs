using System;
using System.Collections.Generic;
using OpenEV.Platform.Imaging.QuickTime;
using OpenEV.Platform.Toolbox;
using Sdcb.FFmpeg.Raw;

namespace OpenEV.Override.Game;

// Decodes a QuickTime movie's voice track (QCELP 'Qclp' / QDesign 'QDMC') to
// interleaved s16 PCM through FFmpeg's LGPL decoders (Sdcb.FFmpeg raw bindings;
// natives ship via NuGet on windows-x64, elsewhere a system FFmpeg 7.x is picked
// up if present). 'musi' (QuickTime Music, a MIDI-style note track) has no
// decoder anywhere and stays silent. Any failure — missing natives, unknown
// codec, decode error — returns null and the movie simply plays silent, which
// was the pre-audio behaviour.
internal static unsafe class MovieAudioFfmpeg
{
    private static bool _unavailable;   // ffmpeg natives missing — don't retry per movie

    public static MacToolbox.MovieAudioTrack? Decode(byte[] movieBytes)
    {
        if (_unavailable) return null;
        var movie = QuickTimeMovieFile.TryParse(movieBytes);
        if (movie is null) return null;

        foreach (var track in movie.AudioTracks)
        {
            AVCodecID codecId = track.FourCC switch
            {
                "Qclp" => AVCodecID.Qcelp,
                "QDMC" => AVCodecID.Qdmc,
                "QDM2" => AVCodecID.Qdm2,
                _ => AVCodecID.None,
            };
            if (codecId == AVCodecID.None) continue;
            try
            {
                var pcm = DecodeTrack(movieBytes, track, codecId, out int rate, out int channels);
                if (pcm is not null && pcm.Length > 0)
                    return new MacToolbox.MovieAudioTrack(pcm, rate, channels, track.FourCC);
            }
            catch (DllNotFoundException)
            {
                _unavailable = true;
                Console.WriteLine("[QT] ffmpeg natives not found — movie audio disabled");
                return null;
            }
        }
        return null;
    }

    private static short[]? DecodeTrack(byte[] data, QuickTimeMovieFile.QtAudioTrack track,
        AVCodecID codecId, out int rate, out int channels)
    {
        rate = track.SampleRate > 0 ? track.SampleRate : 8000;
        channels = track.Channels;

        AVCodec* codec = ffmpeg.avcodec_find_decoder(codecId);
        if (codec == null) return null;
        AVCodecContext* ctx = ffmpeg.avcodec_alloc_context3(codec);
        if (ctx == null) return null;
        AVPacket* pkt = null;
        AVFrame* frame = null;
        try
        {
            ctx->sample_rate = rate;
            ffmpeg.av_channel_layout_default(&ctx->ch_layout, channels);
            if (track.Extradata.Length > 0)
            {
                ctx->extradata = (byte*)ffmpeg.av_mallocz(
                    (ulong)(track.Extradata.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE));
                fixed (byte* src = track.Extradata)
                    Buffer.MemoryCopy(src, ctx->extradata, track.Extradata.Length, track.Extradata.Length);
                ctx->extradata_size = track.Extradata.Length;
            }
            if (ffmpeg.avcodec_open2(ctx, codec, null) < 0) return null;

            var samples = new List<short>(1 << 16);
            pkt = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();
            foreach (var p in track.Packets)
            {
                if (p.Offset < 0 || p.Size <= 0 || p.Offset + p.Size > data.Length) continue;
                if (ffmpeg.av_new_packet(pkt, p.Size) < 0) break;
                fixed (byte* src = &data[p.Offset])
                    Buffer.MemoryCopy(src, pkt->data, p.Size, p.Size);
                if (ffmpeg.avcodec_send_packet(ctx, pkt) >= 0)
                    while (ffmpeg.avcodec_receive_frame(ctx, frame) >= 0)
                        AppendFrame(frame, samples);
                ffmpeg.av_packet_unref(pkt);
            }
            ffmpeg.avcodec_send_packet(ctx, null);   // drain
            while (ffmpeg.avcodec_receive_frame(ctx, frame) >= 0)
                AppendFrame(frame, samples);

            rate = ctx->sample_rate > 0 ? ctx->sample_rate : rate;
            channels = ctx->ch_layout.nb_channels > 0 ? ctx->ch_layout.nb_channels : channels;
            return samples.ToArray();
        }
        finally
        {
            if (frame != null) ffmpeg.av_frame_free(&frame);
            if (pkt != null) ffmpeg.av_packet_free(&pkt);
            ffmpeg.avcodec_free_context(&ctx);
        }
    }

    // Interleave one decoded frame into `samples` as s16, from any of the sample
    // formats these decoders emit (flt/fltp/s16/s16p).
    private static void AppendFrame(AVFrame* frame, List<short> samples)
    {
        int n = frame->nb_samples;
        int ch = frame->ch_layout.nb_channels;
        if (n <= 0 || ch <= 0) return;
        var fmt = (AVSampleFormat)frame->format;
        for (int i = 0; i < n; i++)
            for (int c = 0; c < ch; c++)
            {
                switch (fmt)
                {
                    case AVSampleFormat.Flt:
                        samples.Add(FloatToS16(((float*)frame->data[0])[i * ch + c]));
                        break;
                    case AVSampleFormat.Fltp:
                        samples.Add(FloatToS16(((float*)frame->data[c])[i]));
                        break;
                    case AVSampleFormat.S16:
                        samples.Add(((short*)frame->data[0])[i * ch + c]);
                        break;
                    case AVSampleFormat.S16p:
                        samples.Add(((short*)frame->data[c])[i]);
                        break;
                    default:
                        return;   // unexpected format — stop rather than emit noise
                }
            }
    }

    private static short FloatToS16(float v) =>
        (short)Math.Clamp((int)MathF.Round(v * 32767f), short.MinValue, short.MaxValue);
}
