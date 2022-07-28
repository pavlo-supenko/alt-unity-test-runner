﻿using System.Text;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using Shared.Processes;
using TestsRunner.Arguments;
using TestsRunner.PlatformRunners;
using TestsTreeParser.Tree;

namespace TestsRunner;

class Program
{
    private static readonly ITestsRunner<AndroidArguments, AndroidDriver<AndroidElement>> AndroidTestsRunner = new AndroidTestsRunner();
    private static readonly ITestsRunner<IosArguments, IOSDriver<IOSElement>> IosTestsRunner = new IosTestRunner();

    private static void Main(string[] args)
    {
        var generalArgumentsReader =
            new ArgumentsReader<GeneralArguments>(args, ArgumentKeys.GeneralKeys,
                ArgumentKeys.GeneralDefaults, ArgumentKeys.GeneralDescriptions);

        var androidArgumentsReader =
            new ArgumentsReader<AndroidArguments>(args, ArgumentKeys.AndroidKeys,
                ArgumentKeys.AndroidDefaults, ArgumentKeys.AndroidDescriptions);

        var iosArgumentsReader =
            new ArgumentsReader<IosArguments>(args, ArgumentKeys.IosKeys,
                ArgumentKeys.IosDefaults, ArgumentKeys.IosDescriptions);

        AndroidTestsRunner.Initialize(generalArgumentsReader, androidArgumentsReader);
        IosTestsRunner.Initialize(generalArgumentsReader, iosArgumentsReader);

        if (generalArgumentsReader[GeneralArguments.Help].Equals("true"))
        {
            var showDefaults = generalArgumentsReader[GeneralArguments.Defaults].Equals("true");
            ShowHelp(generalArgumentsReader, "==== General parameters: ====", showDefaults);
            ShowHelp(androidArgumentsReader, "==== Android parameters: ====", showDefaults);
            ShowHelp(iosArgumentsReader, "==== iOS parameters: ====", showDefaults);
            Console.WriteLine();
            
            return;
        }

        try
        {
            switch (generalArgumentsReader[GeneralArguments.Platform])
            {
                case "android":
                    ExecuteTests(AndroidTestsRunner, generalArgumentsReader);
                    break;
                case "ios":
                    ExecuteTests(IosTestsRunner, generalArgumentsReader);
                    break;
                default:
                    Console.WriteLine("Platform key needed to run test session. Exit from application.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("Something went wrong. Exception: {0}", exception.ToString());
        }
        finally
        {
            // todo: write a little bit properly and safe
            switch (generalArgumentsReader[GeneralArguments.Platform])
            {
                case "android":
                    StopSession(AndroidTestsRunner);
                    break;
                case "ios":
                    StopSession(IosTestsRunner);
                    break;
            }
        }
    }

    private static void StopSession<TArgsEnum, TDriver>(ITestsRunner<TArgsEnum, TDriver> testRunner) where TArgsEnum : Enum
    {
        testRunner.StopAppiumServer();
        testRunner.StopAppiumSession();
    }

    private static void ExecuteTests<TArgsEnum, TDriver>(ITestsRunner<TArgsEnum, TDriver> testRunner, ArgumentsReader<GeneralArguments> generalArgumentsReader) where TArgsEnum : Enum
    {
        if (!testRunner.IsDeviceConnected(out var deviceId))
            return;

        if (generalArgumentsReader[GeneralArguments.SkipPortForward].Equals("false"))
            testRunner.SetupPortForwarding(
                tcpLocalPort:generalArgumentsReader[GeneralArguments.LocalPort],
                tcpDevicePort:generalArgumentsReader[GeneralArguments.DevicePort],
                deviceId: deviceId);
        
        if (generalArgumentsReader[GeneralArguments.SkipServerRun].Equals("false"))
            testRunner.RunAppiumServer();

        if (generalArgumentsReader[GeneralArguments.SkipSessionRun].Equals("false"))
            testRunner.RunAppiumSession(deviceId: deviceId, sleepSeconds: 10);

        if (generalArgumentsReader[GeneralArguments.SkipTests].Equals("false"))
        {
            PrintParsedTestsTree(testsTreeJsonPath: generalArgumentsReader[GeneralArguments.TestsTree]);
            RunTests(
                testsTreeFilePath: generalArgumentsReader[GeneralArguments.TestsTree],
                consoleRunnerPath: generalArgumentsReader[GeneralArguments.NUnitConsoleApplicationPath],
                systemLog: generalArgumentsReader[GeneralArguments.TestSystemOutputLogFilePath]);
        }
    }

    private static void PrintParsedTestsTree(string testsTreeJsonPath)
    {
        var testsTree = TestsTree.DeserializeTree(testsTreeJsonPath);
        var testsList = testsTree.GetTestsInvocationList();

        Console.WriteLine("\nOrder of tests to be run parsed from tree file:");
        foreach (var testName in testsList)
            Console.WriteLine(testName);
        
        Console.WriteLine();
    }

    private static void ShowHelp<TArgsEnum>(ArgumentsReader<TArgsEnum> argumentsReader, string header, bool showDefaults) 
        where TArgsEnum : Enum
    {
        Console.WriteLine(header);
        foreach (TArgsEnum argumentValue in Enum.GetValues(typeof(TArgsEnum)))
        {
            var argumentHelp = argumentsReader.GetHelp(argumentValue);
            Console.WriteLine($"    [{argumentHelp.switchName}]  —  {argumentHelp.description}");
            
            if (showDefaults)
                Console.WriteLine($"        default value:{argumentsReader[argumentValue]}");
        }
    }

    private static void RunTests(string testsTreeFilePath, string consoleRunnerPath, string systemLog)
    {
        ProcessRunner processRunner = new ProcessRunner();
        TestsTree testsTree = TestsTree.DeserializeTree(testsTreeFilePath);
        List<string> testsList = testsTree.GetTestsInvocationList();
        Dictionary<string, bool> testsStatus = new Dictionary<string, bool>();

        using StreamWriter sw = new StreamWriter(systemLog,
            new FileStreamOptions()
            {
                Access = FileAccess.Write,
                Mode = FileMode.OpenOrCreate
            });

        foreach (var testName in testsList)
        {
            Console.WriteLine($"Executing test: {testName}");
            var arguments = $"--test={testName} --teamcity TestsClient.dll";
            var systemOutput = processRunner
                .GetProcessOutput(processRunner.StartProcess(consoleRunnerPath, arguments))
                .ToList();

            foreach (var outputLine in systemOutput) 
                sw.WriteLine(outputLine);
            
            sw.WriteLine();
            testsStatus.Add(testName, IsTestSuccess(systemOutput));
        }
        
        sw.Close();
        
        DrawTestsTreeResult(TestsTree.DeserializeTree(testsTreeFilePath), testsStatus);
    }

    private static bool IsTestSuccess(IEnumerable<string> logText)
    {
        var testResultLine = logText.FirstOrDefault(line => line.Contains("Overall result"));

        if (string.IsNullOrEmpty(testResultLine))
            return false;

        return testResultLine.Contains("Passed");
    }

    private static void DrawTestsTreeResult(TestsTree tree, Dictionary<string, bool> testsSuccessStatus)
    {
        Console.WriteLine("\nTests results:");
        
        var currentIndent = 1;
        foreach (var testName in tree.GetTestsInvocationList())
        {
            var testPrintLine = new StringBuilder();
            var isTestSuccess = testsSuccessStatus[testName];
            var isEnterTest = testName.EndsWith(".Enter");

            if (isEnterTest)
                currentIndent += 4;

            testPrintLine.Append(isTestSuccess ? "+ " : "- ");
            for (int i = 0; i < currentIndent; i++)
                testPrintLine.Append(" ");

            testPrintLine.Append($"|_{testName}");

            if (!isEnterTest)
                currentIndent -= 4;
            
            Console.WriteLine(testPrintLine.ToString());
        }
        
        Console.WriteLine();
    }
}
