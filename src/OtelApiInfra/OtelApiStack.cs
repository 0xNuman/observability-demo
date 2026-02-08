using Amazon.CDK;
using Amazon.CDK.AWS.APS;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Grafana;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SecretsManager;

using Constructs;

namespace OtelApiInfra;

public class OtelApiStack : Stack
{
    public OtelApiStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // 1. VPC with public subnet
        var vpc = CreateVpc();

        // 2. AMP Workspace
        var ampWorkspace = CreateAmpWorkspace();

        // 3. CloudWatch Log Group
        var logGroup = CreateLogGroup();

        // 4. IAM Role for EC2
        var ec2Role = CreateEc2Role(ampWorkspace, logGroup);

        // 5. Security Group
        var securityGroup = CreateSecurityGroup(vpc);

        // 6. Key Pair
        var keyPair = CreateKeyPair();

        // 7. EC2 Instance with UserData
        var instance = CreateEc2Instance(vpc, securityGroup, ec2Role, ampWorkspace, keyPair);

        // 8. AMG Workspace
        var amgWorkspace = CreateAmgWorkspace();

        // Outputs
        CreateOutputs(instance, ampWorkspace, amgWorkspace);
    }

    private Vpc CreateVpc()
    {
        return new Vpc(this, "ObservabilityVpc", new VpcProps
        {
            VpcName = "ObservabilityVpc",
            MaxAzs = 1,
            NatGateways = 0,
            SubnetConfiguration =
            [
                new SubnetConfiguration
                {
                    Name = "ObservabilityPublicSubnet",
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                }
            ]
        });
    }

    private Amazon.CDK.AWS.APS.CfnWorkspace CreateAmpWorkspace()
    {
        return new Amazon.CDK.AWS.APS.CfnWorkspace(this, "ObservabilityAmpWorkspace", new Amazon.CDK.AWS.APS.CfnWorkspaceProps
        {
            Alias = "ObservabilityMetrics",
            Tags =
            [
                new CfnTag { Key = "Name", Value = "ObservabilityAmpWorkspace" },
                new CfnTag { Key = "Project", Value = "Observability" }
            ]
        });
    }

    private LogGroup CreateLogGroup()
    {
        return new LogGroup(this, "ObservabilityLogGroup", new LogGroupProps
        {
            LogGroupName = "/aws/observability/api",
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY
        });
    }

    private Role CreateEc2Role(Amazon.CDK.AWS.APS.CfnWorkspace ampWorkspace, LogGroup logGroup)
    {
        var role = new Role(this, "ObservabilityEc2Role", new RoleProps
        {
            RoleName = "ObservabilityEc2Role",
            AssumedBy = new ServicePrincipal("ec2.amazonaws.com"),
            Description = "IAM role for Observability EC2 instance",
            ManagedPolicies =
            [
                ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMManagedInstanceCore"),
                ManagedPolicy.FromAwsManagedPolicyName("CloudWatchAgentServerPolicy")
            ]
        });

        // AMP remote write permissions
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ObservabilityAmpAccess",
            Effect = Effect.ALLOW,
            Actions =
            [
                "aps:RemoteWrite",
                "aps:GetSeries",
                "aps:GetLabels",
                "aps:GetMetricMetadata",
                "aps:QueryMetrics"
            ],
            Resources = [ampWorkspace.AttrArn]
        }));

        // CloudWatch Logs permissions
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ObservabilityCloudWatchLogs",
            Effect = Effect.ALLOW,
            Actions =
            [
                "logs:CreateLogStream",
                "logs:PutLogEvents",
                "logs:DescribeLogStreams"
            ],
            Resources = [$"{logGroup.LogGroupArn}:*"]
        }));

        // Secrets Manager access for key pair
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ObservabilitySecretsAccess",
            Effect = Effect.ALLOW,
            Actions = ["secretsmanager:GetSecretValue"],
            Resources = [$"arn:aws:secretsmanager:{Region}:{Account}:secret:Observability*"]
        }));

        return role;
    }

    private SecurityGroup CreateSecurityGroup(IVpc vpc)
    {
        var sg = new SecurityGroup(this, "ObservabilitySecurityGroup", new SecurityGroupProps
        {
            Vpc = vpc,
            SecurityGroupName = "ObservabilitySecurityGroup",
            Description = "Security group for Observability demo EC2 instance",
            AllowAllOutbound = true,
        });

        // HTTP
        sg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80), "HTTP access for API");

        // HTTPS
        sg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "HTTPS access for API");

        // RDP
        sg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(3389), "RDP access for management");

        // Web Deploy
        sg.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(8172), "Web Deploy for Visual Studio");

        return sg;
    }

    private CfnKeyPair CreateKeyPair()
    {
        var keyPair = new CfnKeyPair(this, "ObservabilityKeyPair", new CfnKeyPairProps
        {
            KeyName = "ObservabilityKeyPair",
            KeyType = "rsa",
            KeyFormat = "pem",
            Tags =
            [
                new CfnTag { Key = "Name", Value = "ObservabilityKeyPair" },
                new CfnTag { Key = "Project", Value = "Observability" }
            ]
        });

        // Store private key in Secrets Manager
        var secret = new Secret(this, "ObservabilityKeyPairSecret", new SecretProps
        {
            SecretName = "ObservabilityKeyPairSecret",
            Description = "Private key for Observability EC2 instance",
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Output instructions for retrieving the key
        _ = new CfnOutput(this, "KeyPairRetrievalCommand", new CfnOutputProps
        {
            Description = "AWS CLI command to retrieve private key",
            Value = $"aws ec2 describe-key-pairs --key-names ObservabilityKeyPair --query 'KeyPairs[0].KeyPairId' --output text | " +
                    $"xargs -I {{}} aws ssm get-parameter --name /ec2/keypair/{{}} --with-decryption --query Parameter.Value --output text > ObservabilityKeyPair.pem"
        });

        return keyPair;
    }

    private Instance_ CreateEc2Instance(IVpc vpc, SecurityGroup sg, Role role, Amazon.CDK.AWS.APS.CfnWorkspace ampWorkspace, CfnKeyPair keyPair)
    {
        var userData = UserData.ForWindows();

        // PowerShell script for complete automation
        userData.AddCommands(
            "# Observability Demo - EC2 Setup Script",
            "Write-Host '========================================' -ForegroundColor Cyan",
            "Write-Host 'Starting Observability Demo Setup' -ForegroundColor Cyan",
            "Write-Host '========================================' -ForegroundColor Cyan",
            "",
            "# Set execution policy",
            "Set-ExecutionPolicy Bypass -Scope Process -Force",
            "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072",
            "",
            "# Create temp directory",
            "New-Item -Path C:\\Temp -ItemType Directory -Force | Out-Null",
            "",
            "# ===== STEP 1: Install IIS =====",
            "Write-Host '[1 / 6] Installing IIS and ASP.NET...' -ForegroundColor Yellow",
            "Install-WindowsFeature -Name Web-Server,Web-Asp-Net45,Web-ISAPI-Ext,Web-ISAPI-Filter,Web-Net-Ext45,Web-Mgmt-Console,Web-Mgmt-Service -IncludeManagementTools",
            "",
            "# Enable remote management",
            "Set-ItemProperty -Path HKLM:\\SOFTWARE\\Microsoft\\WebManagement\\Server -Name EnableRemoteManagement -Value 1",
            "Set-Service -Name WMSVC -StartupType Automatic",
            "Start-Service -Name WMSVC",
            "",
            "# ===== STEP 2: Install .NET 10 Hosting Bundle =====",
            "Write-Host '[2 / 6] Installing.NET 10 Hosting Bundle...' -ForegroundColor Yellow",
            "$dotnetUrl = 'https://download.visualstudio.microsoft.com/download/pr/5e4d9c93-283e-4b66-bcb5-b8b8f6e3d5a2/9a1f3e5e1e1f9c7e5d7f3c1e5b3a9f5e/dotnet-hosting-10.0.1-win.exe'",
            "$dotnetInstaller = 'C:\\Temp\\dotnet-hosting.exe'",
            "",
            "try {",
            "    Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing -TimeoutSec 300",
            "    Start-Process -FilePath $dotnetInstaller -Args '/install /quiet /norestart' -Wait -NoNewWindow",
            "    Write-Host '.NET 10 Hosting Bundle installed successfully' -ForegroundColor Green",
            "} catch {",
            "    Write-Host 'Error installing .NET 10: $_' -ForegroundColor Red",
            "}",
            "",
            "# Restart IIS to load new modules",
            "Write-Host 'Restarting IIS...' -ForegroundColor Yellow",
            "iisreset",
            "",
            "# Verify .NET installation",
            "dotnet --list-runtimes",
            "",
            "# ===== STEP 3: Install Web Deploy =====",
            "Write-Host '[3 / 6] Installing Web Deploy 4.0...' -ForegroundColor Yellow",
            "$webDeployUrl = 'https://download.microsoft.com/download/0/1/D/01DC28EA-638C-4A22-A57B-4CEF97755C6C/WebDeploy_amd64_en-US.msi'",
            "$webDeployInstaller = 'C:\\Temp\\WebDeploy.msi'",
            "",
            "try {",
            "    Invoke-WebRequest -Uri $webDeployUrl -OutFile $webDeployInstaller -UseBasicParsing",
            "    Start-Process msiexec.exe -Args \"/i $webDeployInstaller /quiet /norestart ADDLOCAL=ALL\" -Wait -NoNewWindow",
            "    Set-Service -Name MsDepSvc -StartupType Automatic",
            "    Start-Service -Name MsDepSvc",
            "    Write-Host 'Web Deploy installed successfully' -ForegroundColor Green",
            "} catch {",
            "    Write-Host 'Error installing Web Deploy: $_' -ForegroundColor Red",
            "}",
            "",
            "# Configure Web Deploy firewall rule (already in Security Group, but adding Windows Firewall rule)",
            "New-NetFirewallRule -DisplayName 'ObservabilityWebDeploy' -Direction Inbound -Protocol TCP -LocalPort 8172 -Action Allow -ErrorAction SilentlyContinue",
            "",
            "# ===== STEP 4: Install ADOT Collector =====",
            "Write-Host '[4 / 6] Installing AWS ADOT Collector...' -ForegroundColor Yellow",
            "$adotUrl = 'https://aws-otel-collector.s3.amazonaws.com/windows/amd64/latest/aws-otel-collector.msi'",
            "$adotInstaller = 'C:\\Temp\\adot-collector.msi'",
            "",
            "try {",
            "    Invoke-WebRequest -Uri $adotUrl -OutFile $adotInstaller -UseBasicParsing",
            "    Start-Process msiexec.exe -Args \"/i $adotInstaller /quiet /norestart\" -Wait -NoNewWindow",
            "    Write-Host 'ADOT Collector installed successfully' -ForegroundColor Green",
            "} catch {",
            "    Write-Host 'Error installing ADOT Collector: $_' -ForegroundColor Red",
            "}",
            "",
            "# ===== STEP 5: Configure ADOT Collector =====",
            "Write-Host '[5 / 6] Configuring ADOT Collector...' -ForegroundColor Yellow",
            "$adotConfigPath = 'C:\\ProgramData\\Amazon\\ADOT\\config.yaml'",
            "New-Item -Path (Split-Path $adotConfigPath) -ItemType Directory -Force | Out-Null",
            "",
            "$adotConfig = @\"",
            "receivers:",
            "  otlp:",
            "    protocols:",
            "      grpc:",
            "        endpoint: 0.0.0.0:4317",
            "      http:",
            "        endpoint: 0.0.0.0:4318",
            "",
            "processors:",
            "  batch:",
            "    timeout: 10s",
            "    send_batch_size: 1024",
            "  memory_limiter:",
            "    limit_mib: 512",
            "    spike_limit_mib: 128",
            "    check_interval: 5s",
            "",
            "exporters:",
            "  prometheusremotewrite:",
            $"    endpoint: {ampWorkspace.AttrPrometheusEndpoint}api/v1/remote_write",
            "    auth:",
            "      authenticator: sigv4auth",
            "    resource_to_telemetry_conversion:",
            "      enabled: true",
            "  awscloudwatchlogs:",
            "    log_group_name: /aws/observability/api",
            $"    region: {Region}",
            "    log_stream_name: observability-api-stream",
            "",
            "extensions:",
            "  sigv4auth:",
            $"    region: {Region}",
            "    service: aps",
            "  health_check:",
            "    endpoint: 0.0.0.0:13133",
            "",
            "service:",
            "  extensions: [sigv4auth, health_check]",
            "  pipelines:",
            "    metrics:",
            "      receivers: [otlp]",
            "      processors: [memory_limiter, batch]",
            "      exporters: [prometheusremotewrite]",
            "    logs:",
            "      receivers: [otlp]",
            "      processors: [memory_limiter, batch]",
            "      exporters: [awscloudwatchlogs]",
            "  telemetry:",
            "    logs:",
            "      level: info",
            "\"@",
            "",
            "$adotConfig | Out-File -FilePath $adotConfigPath -Encoding UTF8",
            "Write-Host 'ADOT config created at:' $adotConfigPath -ForegroundColor Green",
            "",
            "# Install and start ADOT as Windows service",
            "$adotExe = 'C:\\Program Files\\Amazon\\AwsOtelCollector\\aws-otel-collector.exe'",
            "if (Test-Path $adotExe) {",
            "    try {",
            "        New-Service -Name 'ObservabilityAWSCollector' -BinaryPathName \"\\`\"$adotExe\\`\" --config=\\`\"$adotConfigPath\\`\"\" -DisplayName 'Observability AWS OTel Collector' -StartupType Automatic -ErrorAction Stop",
            "        Start-Service -Name 'ObservabilityAWSCollector'",
            "        Write-Host 'ADOT Collector service started successfully' -ForegroundColor Green",
            "    } catch {",
            "        Write-Host 'Service may already exist or error occurred: $_' -ForegroundColor Yellow",
            "        Start-Service -Name 'ObservabilityAWSCollector' -ErrorAction SilentlyContinue",
            "    }",
            "} else {",
            "    Write-Host 'ADOT executable not found at expected path' -ForegroundColor Red",
            "}",
            "",
            "# ===== STEP 6: Setup IIS Site =====",
            "Write-Host '[6 / 6] Setting up IIS site...' -ForegroundColor Yellow",
            "Import-Module WebAdministration",
            "",
            "$siteName = 'ObservabilityApi'",
            "$appPoolName = 'ObservabilityApiPool'",
            "$physicalPath = 'C:\\inetpub\\ObservabilityApi'",
            "",
            "# Create application pool",
            "if (-not (Test-Path \"IIS:\\AppPools\\$appPoolName\")) {",
            "    New-WebAppPool -Name $appPoolName",
            "    Set-ItemProperty \"IIS:\\AppPools\\$appPoolName\" -Name managedRuntimeVersion -Value ''",
            "    Set-ItemProperty \"IIS:\\AppPools\\$appPoolName\" -Name processModel.identityType -Value 'ApplicationPoolIdentity'",
            "    Write-Host 'App pool created:' $appPoolName -ForegroundColor Green",
            "}",
            "",
            "# Create physical directory",
            "New-Item -Path $physicalPath -ItemType Directory -Force | Out-Null",
            "",
            "# Create placeholder index.html",
            "$placeholderHtml = @\"",
            "<!DOCTYPE html>",
            "<html>",
            "<head><title>Observability API - Ready for Deployment</title></head>",
            "<body style='font-family: Arial; padding: 50px; text-align: center;'>",
            "    <h1 style='color: #232F3E;'>Observability API</h1>",
            "    <p style='color: #FF9900; font-size: 24px;'>Ready for deployment from Visual Studio</p>",
            "    <hr/>",
            "    <p>ADOT Collector running on ports 4317 (gRPC) and 4318 (HTTP)</p>",
            "    <p>Deploy your .NET 10 API here to start sending telemetry</p>",
            "</body>",
            "</html>",
            "\"@",
            "$placeholderHtml | Out-File -FilePath \"$physicalPath\\index.html\" -Encoding UTF8",
            "",
            "# Remove default website if exists",
            "if (Get-Website -Name 'Default Web Site' -ErrorAction SilentlyContinue) {",
            "    Remove-Website -Name 'Default Web Site'",
            "}",
            "",
            "# Create IIS website",
            "if (-not (Get-Website -Name $siteName -ErrorAction SilentlyContinue)) {",
            "    New-Website -Name $siteName -PhysicalPath $physicalPath -ApplicationPool $appPoolName -Port 80 -Force",
            "    Write-Host 'IIS site created:' $siteName -ForegroundColor Green",
            "}",
            "",
            "# Set proper permissions",
            "$acl = Get-Acl $physicalPath",
            "$ar = New-Object System.Security.AccessControl.FileSystemAccessRule('IIS_IUSRS', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')",
            "$acl.SetAccessRule($ar)",
            "Set-Acl $physicalPath $acl",
            "",
            "# Restart IIS",
            "iisreset",
            "",
            "# ===== SETUP COMPLETE =====",
            "Write-Host '========================================' -ForegroundColor Cyan",
            "Write-Host 'Observability Demo Setup Complete!' -ForegroundColor Green",
            "Write-Host '========================================' -ForegroundColor Cyan",
            "Write-Host 'Summary:' -ForegroundColor White",
            "Write-Host '- IIS Site: ObservabilityApi' -ForegroundColor White",
            "Write-Host '- Physical Path: C:\\inetpub\\ObservabilityApi' -ForegroundColor White",
            "Write-Host '- ADOT Collector: Running on ports 4317/4318' -ForegroundColor White",
            "Write-Host '- Web Deploy: Enabled on port 8172' -ForegroundColor White",
            "Write-Host 'Next Steps:' -ForegroundColor Yellow",
            "Write-Host '1. RDP to this instance' -ForegroundColor Yellow",
            "Write-Host '2. Deploy your .NET 10 API from Visual Studio' -ForegroundColor Yellow",
            "Write-Host '3. Configure your API to send OTel to localhost:4317' -ForegroundColor Yellow",
            "Write-Host '4. View metrics in Grafana dashboard' -ForegroundColor Yellow",
            "Write-Host '========================================' -ForegroundColor Cyan",
            "",
            "# Signal completion to CloudFormation",
            "Write-Host 'Signaling stack completion...' -ForegroundColor Cyan"
        );

        var instance = new Instance_(this, "ObservabilityApiInstance", new InstanceProps
        {
            Vpc = vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            InstanceType = new InstanceType("t3.small"),
            MachineImage = MachineImage.LatestWindows(WindowsVersion.WINDOWS_SERVER_2025_ENGLISH_FULL_BASE),
            SecurityGroup = sg,
            Role = role,
            UserData = userData,
            KeyPair = KeyPair.FromKeyPairAttributes(this, "ImportedKeyPair", new KeyPairAttributes
            {
                KeyPairName = keyPair.KeyName!,
                Type = KeyPairType.RSA
            }),
            BlockDevices =
            [
                new BlockDevice
                {
                    DeviceName = "/dev/sda1",
                    Volume = BlockDeviceVolume.Ebs(50, new EbsDeviceOptions
                    {
                        VolumeType = EbsDeviceVolumeType.GP3,
                        DeleteOnTermination = true
                    })
                }
            ]
        });

        return instance;
    }

    private Amazon.CDK.AWS.Grafana.CfnWorkspace CreateAmgWorkspace()
    {
        var amgRole = new Role(this, "ObservabilityGrafanaRole", new RoleProps
        {
            RoleName = "ObservabilityGrafanaRole",
            AssumedBy = new ServicePrincipal("grafana.amazonaws.com"),
            Description = "IAM role for Observability Grafana workspace",
            ManagedPolicies =
            [
                ManagedPolicy.FromAwsManagedPolicyName("AmazonPrometheusQueryAccess"),
                ManagedPolicy.FromAwsManagedPolicyName("AmazonPrometheusRemoteWriteAccess")
            ]
        });

        // Add AMP-specific permissions
        amgRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ObservabilityGrafanaAmpAccess",
            Effect = Effect.ALLOW,
            Actions =
            [
                "aps:ListWorkspaces",
                "aps:DescribeWorkspace",
                "aps:QueryMetrics",
                "aps:GetLabels",
                "aps:GetSeries",
                "aps:GetMetricMetadata"
            ],
            Resources = ["*"]
        }));

        var amgWorkspace = new Amazon.CDK.AWS.Grafana.CfnWorkspace(this, "ObservabilityGrafanaWorkspace", new Amazon.CDK.AWS.Grafana.CfnWorkspaceProps
        {
            Name = "ObservabilityGrafanaWorkspace",
            AccountAccessType = "CURRENT_ACCOUNT",
            AuthenticationProviders = ["AWS_SSO"],
            PermissionType = "SERVICE_MANAGED",
            RoleArn = amgRole.RoleArn,
            DataSources = ["PROMETHEUS"],
            Description = "Grafana workspace for Observability demo dashboards",
        });

        return amgWorkspace;
    }

    private void CreateOutputs(Instance_ instance, Amazon.CDK.AWS.APS.CfnWorkspace ampWorkspace, Amazon.CDK.AWS.Grafana.CfnWorkspace amgWorkspace)
    {
        _ = new CfnOutput(this, "ObservabilityInstanceId", new CfnOutputProps
        {
            Description = "EC2 Instance ID",
            Value = instance.InstanceId,
            ExportName = "ObservabilityInstanceId"
        });

        _ = new CfnOutput(this, "ObservabilityInstancePublicIp", new CfnOutputProps
        {
            Description = "EC2 Instance Public IP",
            Value = instance.InstancePublicIp,
            ExportName = "ObservabilityInstancePublicIp"
        });

        _ = new CfnOutput(this, "ObservabilityApiEndpoint", new CfnOutputProps
        {
            Description = "API Endpoint URL",
            Value = $"http://{instance.InstancePublicIp}",
            ExportName = "ObservabilityApiEndpoint"
        });

        _ = new CfnOutput(this, "ObservabilityRdpCommand", new CfnOutputProps
        {
            Description = "RDP connection command",
            Value = $"mstsc /v:{instance.InstancePublicIp}"
        });

        _ = new CfnOutput(this, "ObservabilityAmpWorkspaceId", new CfnOutputProps
        {
            Description = "Amazon Managed Prometheus Workspace ID",
            Value = ampWorkspace.AttrWorkspaceId,
            ExportName = "ObservabilityAmpWorkspaceId"
        });

        _ = new CfnOutput(this, "ObservabilityAmpEndpoint", new CfnOutputProps
        {
            Description = "AMP Prometheus Endpoint",
            Value = ampWorkspace.AttrPrometheusEndpoint,
            ExportName = "ObservabilityAmpEndpoint"
        });

        _ = new CfnOutput(this, "ObservabilityAmpRemoteWriteUrl", new CfnOutputProps
        {
            Description = "AMP Remote Write URL (for manual configuration)",
            Value = $"{ampWorkspace.AttrPrometheusEndpoint}api/v1/remote_write"
        });

        _ = new CfnOutput(this, "ObservabilityGrafanaWorkspaceId", new CfnOutputProps
        {
            Description = "Amazon Managed Grafana Workspace ID",
            Value = amgWorkspace.AttrId,
            ExportName = "ObservabilityGrafanaWorkspaceId"
        });

        _ = new CfnOutput(this, "ObservabilityGrafanaUrl", new CfnOutputProps
        {
            Description = "Grafana Dashboard URL",
            Value = $"https://{amgWorkspace.AttrEndpoint}",
            ExportName = "ObservabilityGrafanaUrl"
        });

        _ = new CfnOutput(this, "ObservabilityCloudWatchLogGroup", new CfnOutputProps
        {
            Description = "CloudWatch Log Group for API logs",
            Value = "/aws/observability/api"
        });

        _ = new CfnOutput(this, "ObservabilityWebDeployInfo", new CfnOutputProps
        {
            Description = "Web Deploy connection info for Visual Studio",
            Value = $"Server: {instance.InstancePublicIp}:8172, Site: ObservabilityApi"
        });
    }
}
