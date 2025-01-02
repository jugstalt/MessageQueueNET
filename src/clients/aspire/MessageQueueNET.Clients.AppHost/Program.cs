var builder = DistributedApplication.CreateBuilder(args);

var mq = builder
            .AddMessageQueueNET("messagequeue")
            .WithBindMountPersistance()
            .Build()
            .WithContainerName("my-messagequeue");

var mqDashboard = builder
            .AddDashboardForMessageQueueNET("messagequeue-dashboard")
            .ConnectToMessageQueue(mq, "mq")
            .ConnectToMessageQueue(mq, "mail", "mail*")
            .WithMaxPollingSeconds(5)
            .Build()
            .WithContainerName("my-messagequeue-dashboard");

builder.Build().Run();

