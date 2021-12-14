﻿using BumbleBee.Code.Application.AzureSDKWrappers.Create.NewAppServicePlan;
using BumbleBee.Code.Application.AzureSDKWrappers.Create.NewBlessedAppService;
using BumbleBee.Code.Application.AzureSDKWrappers.Create.NewResourceGroup;
using BumbleBee.Code.Application.AzureSDKWrappers.GetInputs.AdditionalInformation;
using BumbleBee.Code.Application.AzureSDKWrappers.GetInputs.AppServiceName;
using BumbleBee.Code.Application.AzureSDKWrappers.GetInputs.AzureRegion;
using BumbleBee.Code.Application.AzureSDKWrappers.Update.SourceControl;
using CommandDotNet;
using MediatR;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System;
using System.Threading.Tasks;

namespace BumbleBee.CommandLineInterface.Commands.Create
{
    public class DjangoApp
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DjangoApp> _logger;
        private static Random _random = new Random();
        private string _appName;
        private Region _region;
        private string _resourceGroupName;
        private string _appServicePlanName;

        public DjangoApp(IMediator mediator, ILogger<DjangoApp> logger)
        {
            _mediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        [DefaultMethod]
        public async Task NewApp([Option(LongName = "name", ShortName = "n", Description = "Name of the App Service")] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = AnsiConsole.Ask<string>("Enter the [green]name[/] of App Service?");
                }

                //var additionalInfo = await _mediator.Send(new GetAdditionalInformationCommand());
                _appName = await _mediator.Send(new GetAppServiceNameCommand(name));
                _region = await _mediator.Send(new GetRegionNameCommand());

                await AnsiConsole.Status()
                             .StartAsync("Processing...", async ctx =>
                             {
                                 _resourceGroupName = $"{_appName}-rsg";
                                 _appServicePlanName = $"{_appName}-asp";

                                 AnsiConsole.MarkupLine($"Creating a new flask app [green]{_appName}[/] in [green]{_region}[/] region..");

                                 // Update the status and spinner
                                 ctx.Status("Deploying new Resource Group");
                                 ctx.Spinner(Spinner.Known.Line);
                                 ctx.SpinnerStyle(Style.Parse("green"));
                                 bool isResourceGroupCreateSuccessful = await CreateResourceGroup();
                                 if (isResourceGroupCreateSuccessful)
                                 {
                                     ctx.Status("Deploying new App Service Plan");
                                     ctx.Spinner(Spinner.Known.Line);
                                     ctx.SpinnerStyle(Style.Parse("green"));
                                     IAppServicePlan appServicePlan = await CreateNewAppServicePlan();

                                     if (appServicePlan != null)
                                     {
                                         ctx.Status("Deploying new App Service");
                                         ctx.Spinner(Spinner.Known.Line);
                                         ctx.SpinnerStyle(Style.Parse("green"));
                                         IWebApp appService = await CreateNewAppService(appServicePlan);

                                         if (appService != null)
                                         {
                                             string repositoryUrl = "https://github.com/Azure-Samples/python-docs-hello-django";
                                             ctx.Status($"Deploying code from repository..");
                                             ctx.Spinner(Spinner.Known.Line);
                                             ctx.SpinnerStyle(Style.Parse("green"));
                                             await UpdateSourceControl(repositoryUrl);
                                             Console.WriteLine();
                                             AnsiConsole.MarkupLine($"You can browse to the app using [green]https://{appService.DefaultHostName}[/]");
                                         }
                                     }
                                 }
                             });
            }
            catch (Exception ex)
            {
                throw;
            }

            return;
            //try
            //{
            //
        }

        private async Task UpdateSourceControl(string repositoryUrl)
        {
            await _mediator.Send(new UpdateSourceControlCommand()
            {
                ResourceGroupName = _resourceGroupName,
                AppServiceName = _appName,
                SiteSourceControlInner = new SiteSourceControlInner()
                {
                    IsManualIntegration = true,
                    RepoUrl = repositoryUrl,
                    Branch = "main"
                }
            });
        }

        private async Task<IWebApp> CreateNewAppService(IAppServicePlan appServicePlan)
        {
            return await _mediator.Send(new CreateNewAppServiceWithBlessedImageCommand()
            {
                ResourceGroupName = _resourceGroupName,
                AppServicePlan = appServicePlan,
                AppServiceName = _appName,
                AzureRegion = _region
            });
        }

        private async Task<IAppServicePlan> CreateNewAppServicePlan()
        {
            return await _mediator.Send(new CreateNewAppServicePlanCommand()
            {
                ResourceGroupName = _resourceGroupName,
                AppServicePlanName = _appServicePlanName,
                AzureRegion = _region,
            });
        }

        private async Task<bool> CreateResourceGroup()
        {
            return await _mediator.Send(new CreateNewResourceGroupCommand()
            {
                ResourceGroupName = _resourceGroupName,
                AzureRegion = _region,
            });
        }
    }
}