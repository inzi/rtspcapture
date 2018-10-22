using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtsp;
using RTSPSnapshotMaker.RawFramesDecoding;
using RTSPSnapshotMaker.RawFramesDecoding.DecodedFrames;
using RTSPSnapshotMaker.RawFramesDecoding.FFmpeg;

namespace RTSPSnapshotMaker
{
    class Program
    {
        private static readonly Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder> _videoDecodersMap =
           new Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder>();
        private static byte[] _decodedFrameBuffer = new byte[0];
        static int intervalMs = 5000;
        static int lastTimeSnapshotSaved;
        static string SnapshotPath;

        public class Options
        {
            [Option('u', "uri", Required = true, HelpText = "RTSP URI")]
            public Uri Uri { get; set; }

            [Option('p', "path", Required = true, HelpText = "Path where snapshots should be saved")]
            public string Path { get; set; }

            [Option('i', "interval", Required = false, HelpText = "Snapshots saving interval in seconds")]
            public int Interval { get; set; } = 5;
        }

        static void Main(string[] args)
        {

            Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                if (!options.Path.EndsWith(@"\")) options.Path += @"\";

                var cancellationTokenSource = new CancellationTokenSource();

                Task makeSnapshotsTask = ConnectAsync(options, cancellationTokenSource.Token);

                Console.ReadKey();

                cancellationTokenSource.Cancel();
                makeSnapshotsTask.Wait();
            })
            .WithNotParsed(options =>
            {
                Console.WriteLine("Usage example: RTSPSnapshotMaker.exe " +
                                  "-u rtsp://admin:123456@192.168.1.77:554/ucast/11 " +
                                  "-p S:\\Temp");
            });

        }

        private static void OnFrameReceived(object sender, RawFrame rawFrame)
        {


            if (!(rawFrame is RawVideoFrame rawVideoFrame))
                return;

            FFmpegVideoDecoder decoder = GetDecoderForFrame(rawVideoFrame);
            if (!decoder.TryDecode(rawVideoFrame, out DecodedVideoFrameParameters decodedFrameParameters))
                return;


            int ticksNow = Environment.TickCount;

            if (Math.Abs(ticksNow - lastTimeSnapshotSaved) < intervalMs) return;

            lastTimeSnapshotSaved = ticksNow;

            int bufferSize;

            bufferSize = decodedFrameParameters.Height *
                         ImageUtils.GetStride(decodedFrameParameters.Width, RawFramesDecoding.PixelFormat.Abgr32);

            if (_decodedFrameBuffer.Length != bufferSize)
                _decodedFrameBuffer = new byte[bufferSize];

            var bufferSegment = new ArraySegment<byte>(_decodedFrameBuffer);

            var postVideoDecodingParameters = new PostVideoDecodingParameters(RectangleF.Empty,
                new Size(decodedFrameParameters.Width, decodedFrameParameters.Height),
                ScalingPolicy.Stretch, RawFramesDecoding.PixelFormat.Bgr24, ScalingQuality.Bicubic);

            IDecodedVideoFrame decodedFrame = decoder.GetDecodedFrame(bufferSegment, postVideoDecodingParameters);

            ArraySegment<byte> frameSegment = decodedFrame.DecodedBytes;
            string snapshotName = decodedFrame.Timestamp.ToString("O").Replace(":", "_") + ".jpg";

            ToImage(decodedFrameParameters.Width, decodedFrameParameters.Height, snapshotName, System.Drawing.Imaging.PixelFormat.Format24bppRgb, frameSegment.Array);

        }


        private static void ToImage(int Width, int Height, string imgName, System.Drawing.Imaging.PixelFormat pixelFormat, byte[] rgbValues)
        {
            Bitmap bitMap = new Bitmap(Width, Height, pixelFormat);

            Rectangle BoundsRect = new Rectangle(0, 0, Width, Height);

            BitmapData bitmapData = bitMap.LockBits(BoundsRect, ImageLockMode.WriteOnly, bitMap.PixelFormat);

            IntPtr _dataPointer = bitmapData.Scan0;

            int _byteData = bitmapData.Stride * bitMap.Height;

            Marshal.Copy(rgbValues, 0, _dataPointer, _byteData);

            bitMap.UnlockBits(bitmapData);

            ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);

            System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.Quality;

            EncoderParameters encoderParams = new EncoderParameters(1);

            EncoderParameter myEncoderParameter = new EncoderParameter(encoder, 50L);

            encoderParams.Param[0] = myEncoderParameter;

            bitMap.Save(SnapshotPath + imgName, jgpEncoder, encoderParams);

            Console.WriteLine($"New frame {imgName} saved");

        }
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        private static FFmpegVideoDecoder GetDecoderForFrame(RawVideoFrame videoFrame)
        {
            FFmpegVideoCodecId codecId = DetectCodecId(videoFrame);
            if (!_videoDecodersMap.TryGetValue(codecId, out FFmpegVideoDecoder decoder))
            {
                decoder = FFmpegVideoDecoder.CreateDecoder(codecId);
                _videoDecodersMap.Add(codecId, decoder);
            }

            return decoder;
        }

        private static FFmpegVideoCodecId DetectCodecId(RawVideoFrame videoFrame)
        {
            if (videoFrame is RawJpegFrame)
                return FFmpegVideoCodecId.MJPEG;
            if (videoFrame is RawH264Frame)
                return FFmpegVideoCodecId.H264;

            throw new ArgumentOutOfRangeException(nameof(videoFrame));
        }
        private static async Task ConnectAsync(Options options, CancellationToken token)
        {
            try
            {
                ConnectionParameters connectionParameters = new ConnectionParameters(options.Uri);
                if (!Directory.Exists(options.Path))
                    Directory.CreateDirectory(options.Path);
                SnapshotPath = options.Path;
                intervalMs = options.Interval * 1000;
                lastTimeSnapshotSaved = Environment.TickCount - intervalMs;


                using (var rtspClient = new RtspClient(connectionParameters))
                {



                    rtspClient.FrameReceived += OnFrameReceived;

                    while (true)
                    {
                        Console.WriteLine("Connecting...");

                        try
                        {
                            await rtspClient.ConnectAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                            continue;
                        }

                        Console.WriteLine("Connected.");

                        try
                        {
                            await rtspClient.ReceiveAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}