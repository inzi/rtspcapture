using CommandLine;
using RtspClientSharp;
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using RtspCapture.processor;
using RtspCapture.RawFramesReceiving;

namespace RtspCapture
{
    class Program
    {

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
                    .WithParsed(StartCapture)
                    .WithNotParsed(options =>
                    {
                        Console.WriteLine("Usage example: MjpegSnapshotsMaker.exe " +
                                      "-u rtsp://admin:123456@192.168.1.77:554/ucast/11 " +
                                      "-p S:\\Temp");
                    });

            Console.WriteLine("Press any key to cancel");
            Console.ReadKey(false);
        }

        private static void StartCapture(Options options)
        {
            if (!Directory.Exists(options.Path))
                Directory.CreateDirectory(options.Path);

            int intervalMs = options.Interval * 1000;
            int lastTimeSnapshotSaved = Environment.TickCount - intervalMs;

            var connectionParameters = new ConnectionParameters(options.Uri);

            var rawFramesSource = new RawFramesSource(connectionParameters);
            rawFramesSource.ConnectionStatusChanged += (sender, status) => Console.WriteLine(status);
            var decodedFrameSource = new DecodedFrameSource();
            decodedFrameSource.FrameReceived += (sender, frame) =>
            {
                int ticksNow = Environment.TickCount;

                if (Math.Abs(ticksNow - lastTimeSnapshotSaved) < intervalMs)
                    return;

                lastTimeSnapshotSaved = ticksNow;

                Bitmap bitmap = frame.GetBitmap();

                string snapshotName = frame.Timestamp.ToString("O").Replace(":", "_") + ".jpg";
                string path = Path.Combine(options.Path, snapshotName);

                bitmap.Save(path, ImageFormat.Jpeg);

                Console.WriteLine($"[{DateTime.UtcNow}] Snapshot is saved to {snapshotName}");
            };

            decodedFrameSource.SetRawFramesSource(rawFramesSource);

            rawFramesSource.Start();
        }
    }
}
