using System;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Toolbox;

// QuickDraw primitives wired to enqueue Canvas closures via the Toolbox bridge draw
// queue. Color/pen state lives here; the host drains the queue each frame onto
// the persistent virtual target (an Rgba8Image).
public static partial class MacToolbox
{
    private static RgbaColor _activeForeColor = RgbaColor.Black;
    // QuickDraw background colour (BackColor). Mac TETextBox erases its rect to
    // this before drawing the text; default is white (the Mac port default).
    private static RgbaColor _activeBackColor = RgbaColor.White;
    private static int _penX, _penY;
    private static int _penW = 1, _penH = 1;   // QuickDraw pen rect (PenSize / PenNormal)
    private static int _textSize = 12;
    private static int _textFont = 3;   // Mac font family ID 3 = Geneva (the game sets TextFont(3) before every text draw)
    private static int _textFace = 0;

    /// QuickDraw ForeColor — Mac indexed colour. Stores both the index (for
    /// ResolveForeColor) and the resolved RgbaColor (for DrawString/line draws).
    public static void ForeColor(int color)
    {
        _foreColor = color;
        _activeForeColor = MapQuickDrawColorIndex(color);
    }

    /// Map a classic Inside-Macintosh QuickDraw colorConstant (the values the
    /// decompile + Ports actually pass) to an RgbaColor.
    // Base RGB values match the XNA Color constants the old GPU path used (XNA Green is
    // HTML green 0,128,0 — not lime — preserved deliberately). The result is run through
    // Gamma.Correct (Mac DAC ramp, see Gamma.cs) so these directly-painted QuickDraw
    // colours brighten by the same curve as the decoded sprite/PICT art. Pure 0/255
    // primaries stay byte-identical; only greenColor shifts (0,128,0 -> 0,145,0).
    internal static RgbaColor MapQuickDrawColorIndex(int color) => Gamma.Correct(color switch
    {
        0x21  => new RgbaColor(0, 0, 0),        // blackColor   (33)
        0x1e  => new RgbaColor(255, 255, 255),  // whiteColor   (30)
        0xcd  => new RgbaColor(255, 0, 0),      // redColor     (205)
        0x155 => new RgbaColor(0, 128, 0),      // greenColor   (341) — XNA Color.Green
        0x199 => new RgbaColor(0, 0, 255),      // blueColor    (409)
        0x111 => new RgbaColor(0, 255, 255),    // cyanColor    (273)
        0x89  => new RgbaColor(255, 0, 255),    // magentaColor (137)
        0x45  => new RgbaColor(255, 255, 0),    // yellowColor  (69)
        _     => new RgbaColor(0, 0, 0),
    });

    /// QuickDraw RGBForeColor — direct 24-bit foreground. Mac packs
    /// r/g/b into three shorts at an RGBColor pointer; the ported
    /// callers collapsed that to a uint (0xRRGGBB). The colour is run through
    /// Gamma.Correct (Mac DAC ramp, see Gamma.cs) so the directly-painted UI tints
    /// this carries — HUD bars, radar blips, dialog/pilot-info text (UiColors.*) —
    /// brighten by the same curve as the decoded art. See Gamma.
    public static void RGBForeColor(uint rgb)
    {
        _activeForeColor = Gamma.Correct(new RgbaColor(
            (byte)((rgb >> 16) & 0xff),
            (byte)((rgb >>  8) & 0xff),
            (byte)( rgb        & 0xff)));
    }

    public static void MoveTo(int x, int y) { _penX = x; _penY = y; }
    public static void TextFont(int fontId)  { _textFont = fontId; }
    public static void TextSize(int size)    { _textSize = size; }
    public static void TextFace(int face)    { _textFace = face; }

    /// Map the active Mac font family ID (_textFont) to a FontSystem.
    ///   ID 0            = system  → SystemFont (Chicago — bundled ChicagoFLF)
    ///   ID 20  (0x14) = Times    → TimesFont    (Windows Times New Roman)
    ///   ID 2020(0x7e4)= Sillycon → SillyconFont (the bundled sfnt 9295)
    ///   ID 3 / everything else   = Geneva       → Font (real Geneva or the
    ///                                             bundled Grand9K substitute)
    /// Each falls back to the default `Font` when its face is unavailable
    /// (font 0 then re-adds Chicago's weight via the faux-bold double-draw).
    private static SoftwareFont? ResolveFont() => ResolveFontId(_textFont);

    /// Font family ID → face, independent of the port's active _textFont — the
    /// styled-text renderer (DrawStyledTextBox) resolves each 'styl' run with this.
    internal static SoftwareFont? ResolveFontId(int fontId) => fontId switch
    {
        0     => SystemFont ?? Font,     // Mac font family ID 0 = system (Chicago)
        0x14  => TimesFont ?? Font,      // Mac font family ID 20 = Times
        0x7e4 => SillyconFont ?? Font,   // Mac font family ID 2020 = Sillycon
        _     => Font,                   // Geneva (ID 3) + default UI text
    };

    /// Managed-rect FrameRoundRect — {top,left,bottom,right} short[4]. Draws the same 3px
    /// default-button ring as the scalar-arg overload.
    public static void FrameRoundRect(short[] rect, int ovalW, int ovalH)
    {
        if (rect is null || rect.Length < 4) return;
        FrameRoundRectCore(RectFromShorts(rect), ovalW, ovalH);
    }

    private static void FrameRoundRectCore(RectI r, int ovalW, int ovalH)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        var color = _activeForeColor;
        EnqueueDraw(c => DrawRoundRectFrame(c, r, ovalW, ovalH, color));
    }

    /// The rounded-rect frame raster (Mac PenSize(3,3) default-button outline):
    /// per scanline, fill the band between the OUTER round rect and the same
    /// round rect inset by the pen (inner corner radius = outer − pen), which is
    /// how QuickDraw's FrameRoundRect region is defined. Shared by
    /// FrameRoundRectCore and the Dialog Manager's default-button ring
    /// (DrawDlgButton) so both draw identical pixels.
    internal static void DrawRoundRectFrame(Canvas c, RectI r, int ovalW, int ovalH, RgbaColor color)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        const int pen = 3;                       // Mac PenSize(3,3) for the default-button outline
        int rx = System.Math.Max(0, System.Math.Min(ovalW / 2, r.Width / 2));
        int ry = System.Math.Max(0, System.Math.Min(ovalH / 2, r.Height / 2));

        // Horizontal inset of a round rect's edge at row `y` (0 on the straight sides,
        // growing through the quarter-ellipse caps). Pixel-centre sampling.
        static int EdgeInset(int y, int top, int bottom, int radX, int radY)
        {
            double dy;
            if (y < top + radY) dy = (top + radY) - (y + 0.5);
            else if (y >= bottom - radY) dy = (y + 0.5) - (bottom - radY);
            else return 0;
            double t = 1.0 - (dy * dy) / ((double)radY * radY);
            if (t <= 0) return radX;
            return (int)System.Math.Round(radX - radX * System.Math.Sqrt(t));
        }

        var ri = new RectI(r.X + pen, r.Y + pen, r.Width - 2 * pen, r.Height - 2 * pen);
        int irx = System.Math.Max(0, System.Math.Min(rx - pen, ri.Width / 2));
        int iry = System.Math.Max(0, System.Math.Min(ry - pen, ri.Height / 2));
        for (int y = r.Y; y < r.Bottom; y++)
        {
            int o = EdgeInset(y, r.Y, r.Bottom, rx, ry);
            int x0 = r.X + o, x1 = r.Right - o;               // outer span [x0, x1)
            if (x1 <= x0) continue;
            if (ri.Width > 0 && y >= ri.Y && y < ri.Bottom)
            {
                int i = EdgeInset(y, ri.Y, ri.Bottom, irx, iry);
                int ix0 = ri.X + i, ix1 = ri.Right - i;       // hollow span [ix0, ix1)
                if (ix1 > ix0)
                {
                    c.FillRect(new RectI(x0, y, ix0 - x0, 1), color);
                    c.FillRect(new RectI(ix1, y, x1 - ix1, 1), color);
                    continue;
                }
            }
            c.FillRect(new RectI(x0, y, x1 - x0, 1), color);  // cap rows: solid
        }
    }

    /// FrameRoundRect short-arg overload — the {top,left,bottom,right} scalar form the
    /// default-button outlines pass (DrawDefaultButtonOutline / RedrawMissionBbsDialog).
    /// Routes to the same core as the short[] form.
    public static void FrameRoundRect(short top, short left, short bottom, short right,
                                       short ovalW, short ovalH)
    {
        FrameRoundRectCore(new RectI(left, top, right - left, bottom - top), ovalW, ovalH);
    }

    /// QuickDraw InvertRect — flips every pixel under the rect (dst' = 255−dst), the Mac
    /// selection-highlight primitive (mission rows, key-rebind capture, alert text, About
    /// credits, outfitter/commodity selection). XOR-style: two InvertRects on the same
    /// rect cancel, matching the Mac (callers use it as a toggle).
    public static void InvertRect(short top, short left, short bottom, short right)
    {
        int w = right - left, h = bottom - top;
        if (w <= 0 || h <= 0) return;
        var r = new RectI(left, top, w, h);
        EnqueueDraw(c => c.InvertRect(r));
    }

    /// EraseRect for a managed {top,left,bottom,right} short[4] rect.
    public static void EraseRect(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) return;
        EnqueueDraw(c => c.FillRect(rc, RgbaColor.White));
    }

    /// InvertRect for a managed {top,left,bottom,right} short[4] rect.
    public static void InvertRect(short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        InvertRect(rect[0], rect[1], rect[2], rect[3]);
    }

    /// CopyBits blit with C# {top,left,bottom,right} short[4] src/dst rects, so render
    /// code keeps its rects in managed memory rather than EvoMemory pointers.
    public static void CopyBits(int srcBits, int dstBits,
                                  short[] srcRect, short[] dstRect,
                                  int mode, int mask)
    {
        if (srcRect is null || dstRect is null) return;
        // Self-copy of the same buffer+rect = visual no-op; same-buffer/different-rect
        // copies are preserved.
        if (srcBits == dstBits && ReferenceEquals(srcRect, dstRect)) return;
        var srcTex = ResolveTexture(srcBits);
        if (srcTex is null) return;  // unregistered src → silent no-op

        var dst = RectFromShorts(dstRect);
        if (dst.Width <= 0 || dst.Height <= 0) return;

        // Resolve + clamp the source sub-rect to the texture bounds (RectI is
        // immutable, so accumulate into locals then build it once).
        var s0 = RectFromShorts(srcRect);
        int sx, sy, sw, sh;
        if (s0.Width <= 0 || s0.Height <= 0) { sx = 0; sy = 0; sw = srcTex.Width; sh = srcTex.Height; }
        else                                  { sx = s0.X; sy = s0.Y; sw = s0.Width; sh = s0.Height; }
        if (sx + sw > srcTex.Width)  sw = Math.Max(0, srcTex.Width  - sx);
        if (sy + sh > srcTex.Height) sh = Math.Max(0, srcTex.Height - sy);
        // A NEGATIVE src origin means the src rect starts off the top-left of the texture —
        // e.g. the title backdrop (PICT 8000, 832×624) centred in an 800×600 screen has rect
        // {-12,-16,612,816}. Mac CopyBits CLIPS that to the texture; it does NOT bail. Bailing
        // here silently dropped the closed-button save (CopyBits(screen→BACKDROP, backdropRect)),
        // so the title button-reveal animated over already-deployed buttons. Clamp the origin to
        // 0 (shrinking the extent) and carry the SAME clipped margin into the dst, so an identity
        // (src==dst) copy stays 1:1 registered. Non-negative copies are untouched (dst == old).
        int leftClip = sx < 0 ? -sx : 0;
        int topClip  = sy < 0 ? -sy : 0;
        sx += leftClip; sw -= leftClip;
        sy += topClip;  sh -= topClip;
        if (sw <= 0 || sh <= 0) return;

        var srcCapture = new RectI(sx, sy, sw, sh);
        var dstCapture = (leftClip > 0 || topClip > 0)
            ? new RectI(dst.X + leftClip, dst.Y + topClip,
                        Math.Min(dst.Width - leftClip, sw), Math.Min(dst.Height - topClip, sh))
            : dst;
        // Cloak note: blits stay on the INDEXED path (alpha 255) even from the panel-art
        // stash GWorlds — on the Mac all nil-ctab offscreens share the device colour table,
        // so a stash restore under the remapped CLUT is still a raw index copy (SheepShaver
        // shield/fuel-panel capture 2026-07-10: restored track bg = the by-index shade 39,
        // not the inverse-table 64 a converting blit would give).
        EnqueueDrawTo(dstBits, c => c.Blit(srcTex, dstCapture, srcCapture, RgbaColor.White));
    }

    /// Managed-rect parallel of CopyMask(…): the mask is handled by the sprite
    /// texture's own alpha channel (see the int-rect CopyMask note), so this
    /// forwards to the managed-rect CopyBits with the same src/dst semantics.
    public static void CopyMask(int srcBits, int maskBits, int dstBits,
                                 short[] srcRect, short[] maskRect, short[] dstRect)
        => CopyBits(srcBits, dstBits, srcRect, dstRect, 0, 0);

    /// DrawPicture with a C# {top,left,bottom,right} short[4] dst rect instead of an
    /// EvoMemory pointer.
    public static void DrawPicture(int picture, short[] rect)
    {
        if (rect is null || rect.Length < 4) return;
        var rc = RectFromShorts(rect);
        if (rc.Width <= 0 || rc.Height <= 0) return;
        EnqueueDraw(c =>
        {
            var tex = PictResolver?.Invoke(picture);
            if (tex is not null) c.Blit(tex, rc, RgbaColor.White);
            else                 c.FillRect(rc, RgbaColor.White);
        });
    }

    /// The decoded cicn's bounds as {top,left,bottom,right} = {0,0,h,w} for a GetCIcon handle, or
    /// null. Sizes a button dest rect for ports that lost the CIconHandle (can't deref the iconPMap
    /// bounds). Built on the existing GetCIcon scratch pixmap (ResolveCIcon) — no new cicn decode.
    public static short[]? CIconBounds(int ciconHandle)
    {
        var tex = ResolveCIcon(ciconHandle);
        return tex is null ? null : new short[] { 0, 0, (short)tex.Height, (short)tex.Width };
    }

    /// The decoded PICT's bounds as a {top,left,bottom,right} short[4] = {0,0,h,w}, or null
    /// if the picture can't be resolved. Mirrors reading a PicHandle's picFrame; ports that
    /// lost the GetPicture handle (so can't deref picFrame) use this to size a DrawPicture
    /// dest rect. Reuses the host PictResolver (same decode DrawPicture uses).
    public static short[]? PictureBounds(int picId)
    {
        var tex = PictResolver?.Invoke(picId);
        return tex is null ? null : new short[] { 0, 0, (short)tex.Height, (short)tex.Width };
    }

    /// Render a C# string at the pen position in the active font/size/faux-bold. Used for
    /// names/labels sourced from managed records. No-op if Font is unwired or s is empty.
    public static void DrawString(string s)
    {
        var fontSys = ResolveFont();
        if (fontSys is null || string.IsNullOrEmpty(s)) return;
        int pixelSize = _textSize > 0 ? _textSize : 12;
        int penX = _penX;
        int penY = _penY;
        var color = _activeForeColor;
        // QuickDraw txFace bit 0 = bold (TextFace(1) — the red "Hostile"/orange "Forbidden"
        // spaceport status words); systemFont (_textFont==0) is also faux-bold, but ONLY when
        // no real Chicago face is wired (the double-draw approximates Chicago's weight with the
        // light Geneva face; ChicagoFLF is already that heavy).
        // (Italic — txFace bit 1, TextFace(2) chatter — has no faux-slant yet.)
        bool bold = (_textFace & 1) != 0 || (_textFont == 0 && SystemFont is null);
        // Mac QuickDraw draws with the BASELINE at the pen (MoveTo y); SoftwareFont.DrawText
        // takes the text-box TOP and puts the baseline at top + ascent, so top = penY - ascent.
        // Not penY - pixelSize: for a face whose ascent < size (the Sillycon outline: 11 at 14)
        // that lands the baseline (size - ascent) px high and the HUD panel CopyBits crops the
        // glyph top row. Bitmap strikes with ascent == size are unaffected.
        int top = penY - fontSys.Ascent(pixelSize);
        EnqueueDraw(c =>
        {
            fontSys.DrawText(c, s, penX, top, color, pixelSize);
            if (bold) fontSys.DrawText(c, s, penX + 1, top, color, pixelSize);
        });
        // Mac _DrawString advances the pen by the text width (Inside Macintosh:
        // QuickDraw). Must mutate _penX synchronously (not in the deferred closure)
        // so the next no-MoveTo DrawString starts where this one ended — e.g.
        // FormatCredits builds "5,000" from consecutive DrawStrings, and the
        // comm-status line draws label+credits on one pen. The faux-bold +1px
        // shadow does NOT widen the Mac advance, so exclude it.
        _penX += fontSys.MeasureWidth(s, pixelSize);
    }

    /// Mac TextEdit TETextBox — draw `text` word-wrapped inside `rect`
    /// ({top,left,bottom,right} shorts) in the current TextFont/TextSize and fore colour.
    /// `align` 0 = teFlushDefault (left), which is what every EVO call site passes; the
    /// caller already p2cstr'd the text (strlen via FUN_1007613c). TETextBox does NOT do
    /// ParamText ^0..^3 substitution — that is the Dialog Manager's statText job; this is a
    /// raw TextEdit draw, faithful to the Mac. The byte[] overload at MacToolbox.cs stays a
    /// no-op — the player-info dialog's strBuf is a byte[] and binds THERE, not here.
    public static void TETextBox(string text, short[] rect, int align)
    {
        if (rect is null || rect.Length < 4) return;
        TETextBoxCore(text,
            new RectI(rect[1], rect[0], rect[3] - rect[1], rect[2] - rect[0]), align);
    }

    private static void TETextBoxCore(string text, RectI rect, int align)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var fontSys = ResolveFont();
        int pixelSize = _textSize > 0 ? _textSize : 12;
        var color = _activeForeColor;
        // Mac TETextBox ERASES its rect to the background colour before drawing the
        // text. EVO relies on this: the generic alert draws white text on a black
        // BackColor erase, and the spaceport description draws black text on the
        // default white erase then InvertRects the rect → white-on-black. Without
        // the erase the spaceport text was black-on-black → invert → a WHITE BLOCK.
        // Capture bkColor now (the closure runs at flush, after callers restore it).
        var bk = _activeBackColor;
        bool bold = (_textFace & 1) != 0 || (_textFont == 0 && SystemFont is null);   // TextFace(1) bold + systemFont faux-bold only when no real Chicago is wired

        // Greedy word-wrap to the rect width, honouring explicit newlines. Mac TextEdit breaks a
        // word that REACHES the right edge (width >= rect), not only one that exceeds it. That exact
        // edge rule is applied on the 1-bit Mac strike path (register Geneva 9) so the wrap is
        // pixel-exact; the TTF/game path keeps the prior `>` so no audited game layout shifts.
        bool wrapAtEdge = fontSys?.StrikeLineHeight(pixelSize) is not null;
        var lines = new System.Collections.Generic.List<string>();
        if (fontSys is not null && !string.IsNullOrEmpty(text))
        {
            foreach (string para in text.Replace("\r", "\n").Split('\n'))
            {
                if (para.Length == 0) { lines.Add(string.Empty); continue; }
                string cur = string.Empty;
                foreach (string word in para.Split(' '))
                {
                    string trial = cur.Length == 0 ? word : cur + " " + word;
                    int w = fontSys.MeasureWidth(trial, pixelSize);
                    if ((wrapAtEdge ? w >= rect.Width : w > rect.Width) && cur.Length > 0)
                    {
                        lines.Add(cur);
                        cur = word;
                    }
                    else cur = trial;
                }
                lines.Add(cur);
            }
        }

        // Bitmap strike (register Geneva 9) → the true Mac font height (12 for Geneva 9);
        // otherwise the prior size + leading approximation (unchanged for TTF/game text).
        int lineH = fontSys?.StrikeLineHeight(pixelSize) ?? pixelSize + 2;
        int x = rect.X;
        int yTop = rect.Y;
        var box = rect;
        var lineSnapshot = lines.ToArray();
        EnqueueDraw(c =>
        {
            c.FillRect(box, bk);                 // erase to background (Mac TETextBox)
            if (fontSys is null) return;
            for (int li = 0; li < lineSnapshot.Length; li++)
            {
                string ln = lineSnapshot[li];
                if (ln.Length == 0) continue;
                int y = yTop + li * lineH;
                fontSys.DrawText(c, ln, x, y, color, pixelSize);
                if (bold) fontSys.DrawText(c, ln, x + 1, y, color, pixelSize);
            }
        });
    }

}
