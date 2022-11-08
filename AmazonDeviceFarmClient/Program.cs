﻿using Shared.Arguments;
using AmazonDeviceFarmClient.Client;
using AmazonDeviceFarmClient.Arguments;


namespace AmazonDeviceFarmClient;

public static class Program
{
    private static ArgumentsReader<DeviceFarmArgument> argumentsReader;

    private static async Task Main(string[] args)
    {
        argumentsReader = new ArgumentsReader<DeviceFarmArgument>(args, DeviceFarmArgumentValues.Keys,
            DeviceFarmArgumentValues.Defaults, DeviceFarmArgumentValues.Descriptions);

        if (TryShowHelp(argumentsReader))
            return;

        DeviceFarmClient deviceFarmClient = new DeviceFarmClient(
            argumentsReader[DeviceFarmArgument.UserKey],
            argumentsReader[DeviceFarmArgument.UserSecret]);

        if (argumentsReader.IsExist(DeviceFarmArgument.UploadTestPackage))
            await deviceFarmClient.UploadTestPackage(
                argumentsReader[DeviceFarmArgument.TestPackageName],
                argumentsReader[DeviceFarmArgument.ProjectName],
                argumentsReader[DeviceFarmArgument.UploadTestPackage]);
        
        if (argumentsReader.IsExist(DeviceFarmArgument.UploadTestSpecs))
            await deviceFarmClient.UploadTestSpec(
                argumentsReader[DeviceFarmArgument.TestSpecsName],
                argumentsReader[DeviceFarmArgument.ProjectName],
                argumentsReader[DeviceFarmArgument.UploadTestSpecs]);

        bool isAndroid = argumentsReader[DeviceFarmArgument.ApplicationPlatform].Equals("Android");
        ApplicationPlatform platform = isAndroid ? ApplicationPlatform.Android : ApplicationPlatform.Ios;
        
        if (isAndroid)
            await deviceFarmClient.UploadAndroidApplication(
                argumentsReader[DeviceFarmArgument.ApplicationName],
                argumentsReader[DeviceFarmArgument.ProjectName],
                argumentsReader[DeviceFarmArgument.UploadApplication]);
        else
            await deviceFarmClient.UploadIosApplication(
                argumentsReader[DeviceFarmArgument.ApplicationName],
                argumentsReader[DeviceFarmArgument.ProjectName],
                argumentsReader[DeviceFarmArgument.UploadApplication]);
        

        await deviceFarmClient.ScheduleTestRun(
            argumentsReader[DeviceFarmArgument.RunName],
            platform,
            argumentsReader[DeviceFarmArgument.ProjectName],
            argumentsReader[DeviceFarmArgument.DevicePoolName],
            argumentsReader[DeviceFarmArgument.TestSpecsName],
            int.Parse(argumentsReader[DeviceFarmArgument.Timeout]));
    }

    private static bool TryShowHelp(ArgumentsReader<DeviceFarmArgument> argumentsReader)
    {
        if (!argumentsReader.IsTrue(DeviceFarmArgument.Help)) 
            return false;

        var showDefaults = true;
        ArgumentsReader<DeviceFarmArgument>.ShowHelp(argumentsReader, "==== Parameters: ====", showDefaults);
        Console.WriteLine();

        return true;
    }
}