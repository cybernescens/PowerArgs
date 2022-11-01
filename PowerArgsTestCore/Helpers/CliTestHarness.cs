using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using PowerArgs;
using PowerArgs.Cli;

namespace ArgsTests.CLI;

public class CliLKGTestMetadata
{
    public int Paints { get; set; }
}

public class CliTestHarness : ConsoleApp
{
    private int keyFrameCount;
    private ConsoleBitmapVideoWriter? keyFrameRecorder;
    private TestContext testContext;

    public CliTestHarness(TestContext testContext, int w, int h, bool keyframeMode = false) : base(w, h)
    {
        Init(testContext, keyframeMode);
    }

    public CliTestHarness(TestContext testContext, bool keyframeMode = false) { Init(testContext, keyframeMode); }

    public double SecondsBetweenKeyframes { get; set; } = 1;
    private ConsoleBitmapVideoWriter? EffectiveRecorder => keyFrameRecorder ?? Recorder;

    public string TestId => $"{testContext.FullyQualifiedTestClassName}.{testContext.TestName}";

    public string GitRootPath
    {
        get {
            var gitRoot = Assembly.GetExecutingAssembly().Location;
            while (Directory.Exists(Path.Combine(gitRoot, ".git")) == false)
            {
                gitRoot = Path.GetDirectoryName(gitRoot);
            }

            return gitRoot;
        }
    }

    public string CurrentTestRootPath => Path.Combine(GitRootPath, "LKGCliResults", TestId);

    public string CurrentTestLKGPath => Path.Combine(CurrentTestRootPath, "LKG");
    public string CurrentTestTempPath => Path.Combine(CurrentTestRootPath, "TEMP");

    public string CurrentTestRecordingFilePath => Path.Combine(CurrentTestTempPath, "Recording.cv");
    public string CurrentTestMetadataFilePath => Path.Combine(CurrentTestTempPath, "Metadata.json");

    public string CurrentTestRecordingLKGFilePath => Path.Combine(CurrentTestLKGPath, "Recording.cv");
    public string CurrentTestMetadataLKGFilePath => Path.Combine(CurrentTestLKGPath, "Metadata.json");

    private void Init(TestContext testContext, bool keyframeMode = false)
    {
        this.testContext = testContext;
        if (!Directory.Exists(CurrentTestLKGPath)) Directory.CreateDirectory(CurrentTestLKGPath);
        if (!Directory.Exists(CurrentTestTempPath)) Directory.CreateDirectory(CurrentTestTempPath);

        if (keyframeMode)
        {
            keyFrameRecorder = new ConsoleBitmapVideoWriter(s => File.WriteAllText(CurrentTestRecordingFilePath, s));
        }
        else
        {
            Recorder = new ConsoleBitmapVideoWriter(s => File.WriteAllText(CurrentTestRecordingFilePath, s));
        }

        Stopped.SubscribeOnce(
            () => {
                var metadata = new CliLKGTestMetadata {
                    Paints = TotalPaints
                };

                Console.WriteLine("Total paints: " + metadata.Paints);

                var json = JsonConvert.SerializeObject(metadata);
                File.WriteAllText(CurrentTestMetadataFilePath, json);
            });
    }

    public bool TryGetLKGMetadata(out CliLKGTestMetadata metadata) =>
        TryGetMetadata(CurrentTestMetadataLKGFilePath, out metadata);

    public bool TryGetCurrentMetadata(out CliLKGTestMetadata metadata) =>
        TryGetMetadata(CurrentTestMetadataFilePath, out metadata);

    public bool TryGetLKGRecording(out ConsoleBitmapStreamReader reader) =>
        TryGetRecording(CurrentTestRecordingLKGFilePath, out reader);

    public bool TryGetCurrentRecording(out ConsoleBitmapStreamReader reader) =>
        TryGetRecording(CurrentTestRecordingFilePath, out reader);

    public void RecordKeyFrame() =>
        keyFrameRecorder?.WriteFrame(Bitmap, true, TimeSpan.FromSeconds(SecondsBetweenKeyframes * keyFrameCount++));

    public async Task PaintAndRecordKeyFrameAsync()
    {
        await RequestPaintAsync();
        await RequestPaintAsync();

        if (Current == this)
        {
            RecordKeyFrame();
        }
        else
        {
            await InvokeAsync(RecordKeyFrame);
        }
    }

    public void Abandon()
    {
        keyFrameRecorder?.Finish();
    }

    public void PromoteToLKG()
    {
        keyFrameRecorder?.Finish();

        PromoteToLKGInternal();
    }

    public void AssertThisTestMatchesLKG()
    {
        keyFrameRecorder?.Finish();

        if (TryGetLKGMetadata(out var metadata) && TryGetLKGRecording(out var reader))
        {
            reader.InnerStream.Dispose();
            AssertLKGRecordingMatchesCurrentTest();
            Console.WriteLine("LKG matches");
            PromoteToLKGInternal();
        }
        else
        {
            Console.WriteLine("Orignial LKG");
            PromoteToLKGInternal();
        }
    }

    public void AssertThisTestMatchesLKGFirstAndLastFrame()
    {
        if (keyFrameRecorder != null)
        {
            keyFrameRecorder.Finish();
            throw new Exception("You should call the version that checks every frame since you are in keyframe mode");
        }

        if (TryGetLKGMetadata(out var metadata) && TryGetLKGRecording(out var reader))
        {
            reader.InnerStream.Dispose();
            AssertLKGRecordingMatchesCurrentTestFirstAndLast();
            Console.WriteLine("LKG matches");
            PromoteToLKGInternal();
        }
        else
        {
            Console.WriteLine("Original LKG");
            PromoteToLKGInternal();
        }
    }

    private void AssertLKGRecordingMatchesCurrentTest()
    {
        if (TryGetCurrentRecording(out var currentReader) &&
            TryGetLKGRecording(out var lkgReader))
        {
            var currentVideo = currentReader.ReadToEnd();
            var lkgVideo = lkgReader.ReadToEnd();
            currentReader.InnerStream.Close();
            lkgReader.InnerStream.Close();
            Assert.AreEqual(lkgVideo.Frames.Count, currentVideo.Frames.Count, "Frame count does not match");

            for (var i = 0; i < lkgVideo.Frames.Count; i++)
            {
                var lkgFrame = lkgVideo.Frames[i];
                var currentFrame = currentVideo.Frames[i];

                if (lkgFrame.Bitmap.Equals(currentFrame.Bitmap) == false)
                {
                    Assert.Fail("Frames do not match at index " + i);
                }
            }
        }
    }

    private void AssertLKGRecordingMatchesCurrentTestFirstAndLast()
    {
        if (TryGetCurrentRecording(out var currentReader) &&
            TryGetLKGRecording(out var lkgReader))
        {
            var currentVideo = currentReader.ReadToEnd();
            var lkgVideo = lkgReader.ReadToEnd();
            currentReader.InnerStream.Close();
            lkgReader.InnerStream.Close();

            var lkgFirstFrame = lkgVideo.Frames[0];
            var currentFirstFrame = currentVideo.Frames[0];

            var lkgLastFrame = lkgVideo.Frames[lkgVideo.Frames.Count - 1];
            var currentLastFrame = currentVideo.Frames[currentVideo.Frames.Count - 1];

            Assert.AreEqual(lkgFirstFrame.Bitmap, currentFirstFrame.Bitmap);
            Assert.AreEqual(lkgLastFrame.Bitmap, currentLastFrame.Bitmap);
        }
    }

    private bool TryGetMetadata(string path, out CliLKGTestMetadata metadata)
    {
        if (File.Exists(path) == false)
        {
            metadata = null;
            return false;
        }

        var json = File.ReadAllText(path);
        metadata = JsonConvert.DeserializeObject<CliLKGTestMetadata>(json);
        return true;
    }

    private bool TryGetRecording(string path, out ConsoleBitmapStreamReader recordingReader)
    {
        if (File.Exists(path) == false)
        {
            recordingReader = null;
            return false;
        }

        recordingReader = new ConsoleBitmapStreamReader(File.OpenRead(path));
        return true;
    }

    private void PromoteToLKGInternal()
    {
        if (Directory.Exists(CurrentTestLKGPath))
        {
            Directory.Delete(CurrentTestLKGPath, true);
        }

        Directory.Move(CurrentTestTempPath, CurrentTestLKGPath);
    }

    public Point? Find(ConsoleString? text, StringComparison comparison = StringComparison.InvariantCulture) =>
        Find(text, comparison, true);

    public Point? Find(string text, StringComparison comparison = StringComparison.InvariantCulture) =>
        Find(text.ToConsoleString(), comparison, false);

    private Point? Find(ConsoleString? text, StringComparison comparison, bool requireStylesToBeEqual)
    {
        if (text.Contains("\n") || text.Contains("\r"))
        {
            throw new ArgumentException(
                "Text cannot contain newline characters. This function searches the target bitmap line by line.");
        }

        for (var y = 0; y < Bitmap.Height; y++)
        {
            var line = ConsoleString.Empty;
            for (var x = 0; x < Bitmap.Width; x++)
            {
                var pixel = Bitmap.GetPixel(x, y);
                line += pixel.ToConsoleString();
            }

            int index;

            if (requireStylesToBeEqual)
            {
                index = line.IndexOf(text, comparison);
            }
            else
            {
                index = line.ToString().IndexOf(text.ToString(), comparison);
            }

            if (index >= 0)
            {
                return new Point(index, y);
            }
        }

        return null;
    }
}