﻿using System.Reflection;
using Newtonsoft.Json;
using PowerArgs;
using PowerArgs.Cli;
using PowerArgs.Cli.Physics;

namespace Benchmarks;

public class UIBenchmarkData
{
    public float WorkDone { get; set; }

    public static UIBenchmarkData Average(params UIBenchmarkData[] input)
    {
        return new UIBenchmarkData {
            WorkDone = ConsoleMath.Round(input.Select(input => input.WorkDone).Average())
        };
    }
}

public class UIBenchmarkComparison
{
    public string Test { get; set; }
    public float NoiseVariance { get; set; } = .03f;

    public UIBenchmarkData LKG { get; set; }
    public UIBenchmarkData Temp { get; set; }

    public bool HasLKG => LKG != null;

    public float TempPaintsOverLKG => HasLKG ? Temp.WorkDone / LKG.WorkDone : 0;

    public float PaintSpeedup => HasLKG ? Temp.WorkDone / LKG.WorkDone - 1 : 0;

    public float PaintSpeedupPercentage => ConsoleMath.Round(100 * PaintSpeedup, 1);

    public ConsoleString? PaintSpeedupString =>
        PaintSpeedup > 0
            ? (PaintSpeedupPercentage + " %").ToConsoleString(IsPaintWin ? RGB.Green : RGB.Gray)
            : PaintSpeedup < 0
                ? (PaintSpeedupPercentage + " %").ToConsoleString(IsPaintRegression ? RGB.Red : RGB.Gray)
                : HasLKG
                    ? "0 %".ToGray()
                    : ConsoleString.Empty;

    public bool IsPaintRegression => HasLKG ? PaintSpeedup < 0 && Math.Abs(PaintSpeedup) > NoiseVariance : false;

    public bool IsPaintWin => HasLKG ? PaintSpeedup > 0 && PaintSpeedup > NoiseVariance : false;

    public bool IsWin => HasLKG && IsPaintWin;
}

public abstract class UIBenchmark
{
    private string GitRootPath
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

    public string TestId => GetType().FullName;
    private string CurrentTestRootPath => Path.Combine(GitRootPath, "UIBenchmarkResults", TestId);
    private string CurrentTestLKGPath => Path.Combine(CurrentTestRootPath, "LKG");
    private string CurrentTestTempPath => Path.Combine(CurrentTestRootPath, "TEMP");
    public string CurrentTestOutputFilePath => Path.Combine(CurrentTestTempPath, "Output.json");
    public string CurrentTestOutputLKGFilePath => Path.Combine(CurrentTestLKGPath, "Output.json");

    public UIBenchmarkComparison Run()
    {
        MakeSureFoldersExist();
        ConsoleProvider.Current.WindowWidth = 150;
        ConsoleProvider.Current.BufferWidth = 150;
        ConsoleProvider.Current.WindowHeight = 50;

        // throwaway
        var app = new ConsoleApp();
        RunActual(app);

        var output = new UIBenchmarkData[2];
        for (var i = 0; i < output.Length; i++)
            using (var realRunApp = new ConsoleApp())
            {
                var result = RunActual(realRunApp);
                output[i] = new UIBenchmarkData {
                    WorkDone = result
                };
            }

        var avg = UIBenchmarkData.Average(output);

        return Compare(avg);
    }

    private void MakeSureFoldersExist()
    {
        if (Directory.Exists(CurrentTestRootPath) == false)
        {
            Directory.CreateDirectory(CurrentTestRootPath);
        }

        if (Directory.Exists(CurrentTestLKGPath) == false)
        {
            Directory.CreateDirectory(CurrentTestLKGPath);
        }

        if (Directory.Exists(CurrentTestTempPath) == false)
        {
            Directory.CreateDirectory(CurrentTestTempPath);
        }
    }

    public UIBenchmarkComparison Compare(UIBenchmarkData thisTestResults)
    {
        WriteTemp(thisTestResults);
        if (TryLoadLKG(out var lkgResults))
        {
            var ret = new UIBenchmarkComparison { Test = GetType().Name, LKG = lkgResults, Temp = thisTestResults };
            if (ret.IsWin)
            {
                PromoteToLKG();
            }
            else
            {
                File.Delete(CurrentTestOutputFilePath);
            }

            return ret;
        }

        PromoteToLKG();
        return new UIBenchmarkComparison { Test = GetType().Name, Temp = thisTestResults };
    }

    private void WriteTemp(UIBenchmarkData data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(CurrentTestOutputFilePath, json);
    }

    private void PromoteToLKG() { File.Move(CurrentTestOutputFilePath, CurrentTestOutputLKGFilePath, true); }

    private bool TryLoadLKG(out UIBenchmarkData ret)
    {
        if (File.Exists(CurrentTestOutputLKGFilePath))
        {
            var contents = File.ReadAllText(CurrentTestOutputLKGFilePath);
            ret = JsonConvert.DeserializeObject<UIBenchmarkData>(contents);
            return true;
        }

        ret = null;
        return false;
    }

    protected abstract float RunActual(ConsoleApp app);
}