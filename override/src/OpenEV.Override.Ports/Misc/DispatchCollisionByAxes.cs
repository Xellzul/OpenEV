using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007d4d4 (EV Override-11.c lines 53989-54070): reads GlobalState.SpriteLoopStart,
// then SpriteLoopEnd, and dispatches to one of fifteen axis-bucketed collision-pass routines
// keyed on that (start, end) pair.
public static class DispatchCollisionByAxes
{
    public static void Run()
    {
        var selector = GlobalState.SpriteLoopStart;
        if (selector == 1)
        {
            selector = GlobalState.SpriteLoopEnd;
            if (selector != 3)
            {
                if (selector < 3)
                {
                    if (selector == 1)
                    {
                        SpriteCollisionPassBy4cSymmetric.Run();
                    }
                    else if (selector < 1)
                    {
                        if (-1 < selector)
                        {
                            SpriteCollisionPassByZ.Run();
                        }
                    }
                    else
                    {
                        CollisionPass.Run();
                    }
                }
                else if (selector == 5)
                {
                    SpriteCollisionPassBy4cAsymmetricSweep.Run();
                }
                else if (selector < 5)
                {
                    SpriteCollisionPassBy4cAsymmetric.Run();
                }
            }
        }
        else if (selector < 1)
        {
            if (-1 < selector)
            {
                selector = GlobalState.SpriteLoopEnd;
                if (selector != 3)
                {
                    if (selector < 3)
                    {
                        if (selector == 1)
                        {
                            TickSpriteOverlapCallbacks.Run();
                        }
                        else if (selector < 1)
                        {
                            if (-1 < selector)
                            {
                                TickSpriteCollisions.Run();
                            }
                        }
                        else
                        {
                            TickSpriteOverlapBackward.Run();
                        }
                    }
                    else if (selector == 5)
                    {
                        SpriteCollisionPassBy2Asymmetric.Run();
                    }
                    else if (selector < 5)
                    {
                        TickSpriteOverlapDispatchAll.Run();
                    }
                }
            }
        }
        else if (selector < 3)
        {
            selector = GlobalState.SpriteLoopEnd;
            if (selector != 3)
            {
                if (selector < 3)
                {
                    if (selector == 1)
                    {
                        SpriteCollisionPassFullSquareSymmetric.Run();
                    }
                    else if (selector < 1)
                    {
                        if (-1 < selector)
                        {
                            SpriteCollisionPassFullSquareAttackerTarget.Run();
                        }
                    }
                    else
                    {
                        SpriteCollisionPassPrevChainSymmetric.Run();
                    }
                }
                else if (selector == 5)
                {
                    SpriteCollisionPassBothChainsAsymmetric.Run();
                }
                else if (selector < 5)
                {
                    SpriteCollisionPassFullSquareAsymmetric.Run();
                }
            }
        }
    }
}
