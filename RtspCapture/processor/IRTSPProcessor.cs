using System;
using System.Collections.Generic;
using System.Text;
using RtspCapture.RawFramesDecoding.DecodedFrames;

namespace RtspCapture.processor
{
    class IRTSPProcessor
    {
    }

    public interface IVideoSource
    {
        event EventHandler<IDecodedVideoFrame> FrameReceived;

        void SetVideoSize(int width, int height);
    }
}
