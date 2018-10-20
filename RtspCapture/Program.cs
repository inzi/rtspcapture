//using CommandLine;
using RtspClientSharp;
using RtspClientSharp.RawFrames.Video;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using RtspCapture.processor;
using RtspCapture.RawFramesReceiving;

namespace RtspCapture
{
    class Program
    {

        public class Options
        {
            //[Option('u', "uri", Required = true, HelpText = "RTSP URI")]
            public Uri Uri { get; set; }

            //[Option('p', "path", Required = true, HelpText = "Path where snapshots should be saved")]
            public string Path { get; set; }

            //[Option('i', "interval", Required = false, HelpText = "Snapshots saving interval in seconds")]
            public int Interval { get; set; } = 5;
        }
        private static readonly RTSPProcessor rTSPProcessor = new RTSPProcessor();
        //private RawFramesSource _rawFramesSource;

        public event EventHandler<string> StatusChanged;

        //public IVideoSource VideoSource => _realtimeVideoSource;
        static void Main(string[] args)
        {

            Options options = new Options();
            options.Uri = new Uri("rtsp://admin:admin@10.50.1.85/1");
            options.Interval = 5;
            options.Path = @"c:\capture";

            var cancellationtokensource = new CancellationTokenSource();

            //task makesnapshotstask = makesnapshotsasync(options, cancellationtokensource.token);
            StartCapture(options);
            Console.ReadKey();

            cancellationtokensource.Cancel();
            //makesnapshotstask.wait();

            //Parser.Default.ParseArguments<Options>(args)
            //        .WithParsed(options =>
            //        {
            //            var cancellationTokenSource = new CancellationTokenSource();

            //            //Task makeSnapshotsTask = MakeSnapshotsAsync(options, cancellationTokenSource.Token);
            //            StartCapture(options);
            //            Console.ReadKey();

            //            cancellationTokenSource.Cancel();
            //            //makeSnapshotsTask.Wait();
            //        })
            //        .WithNotParsed(options =>
            //        {
            //            Console.WriteLine("Usage example: MjpegSnapshotsMaker.exe " +
            //                          "-u rtsp://admin:123456@192.168.1.77:554/ucast/11 " +
            //                          "-p S:\\Temp");
            //        });

            Console.WriteLine("Press any key to cancel");
            Console.ReadLine();

        }

        private static void StartCapture(Options options)
        {

            System.Diagnostics.Debug.WriteLine("Start Capture");
            //if (_rawFramesSource != null)
            //    return;
            if (!Directory.Exists(options.Path))
                Directory.CreateDirectory(options.Path);

            int intervalMs = options.Interval * 1000;
            int lastTimeSnapshotSaved = Environment.TickCount - intervalMs;

            var connectionParameters = new ConnectionParameters(options.Uri);

            RawFramesSource _rawFramesSource = new RawFramesSource(connectionParameters);
            _rawFramesSource.ConnectionStatusChanged += ConnectionStatusChanged;

            rTSPProcessor.SetRawFramesSource(_rawFramesSource);

            _rawFramesSource.Start();
        }

        private static void ConnectionStatusChanged(object sender, string s)
        {
            //StatusChanged?.Invoke(this, s);
        }

        private static async Task MakeSnapshotsAsync(Options options, CancellationToken token)
        {
            try
            {
                if (!Directory.Exists(options.Path))
                    Directory.CreateDirectory(options.Path);

                int intervalMs = options.Interval * 1000;
                int lastTimeSnapshotSaved = Environment.TickCount - intervalMs;

                var connectionParameters = new ConnectionParameters(options.Uri);
              
                //using (var rtspClient = new RtspClient(connectionParameters))
                //{
                //    rtspClient.FrameReceived += (sender, frame) =>
                //    {

                //        int ticksNow = Environment.TickCount;

                //        if (Math.Abs(ticksNow - lastTimeSnapshotSaved) < intervalMs)
                //            return;

                //        lastTimeSnapshotSaved = ticksNow;

                //        string snapshotName = frame.Timestamp.ToString("O").Replace(":", "_") + ".jpg";
                //        string path = Path.Combine(options.Path, snapshotName);

                //        ArraySegment<byte> frameSegment = frame.FrameSegment;

                //        //var stream = new MemoryStream(frameSegment.Array, 0, frameSegment.Count);

                //        //Image image = Image.FromStream(stream);

                //        //image.Save($"[{DateTime.UtcNow}] Snapshot is saved to {snapshotName}");

                //        Console.WriteLine($"[{DateTime.UtcNow}] Snapshot is saved to {snapshotName}");
                //    };

                //    Console.WriteLine("Connecting...");
                //    await rtspClient.ConnectAsync(token);
                //    Console.WriteLine("Receiving...");
                //    await rtspClient.ReceiveAsync(token);
                //}
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
