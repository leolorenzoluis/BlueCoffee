using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.IO;
using JavaUtils;
using System.IO.Compression;

namespace Zookeeper
{
	public class WorkerRole : RoleEntryPoint
	{
		private string _javaInstallHome;
		private string _jarsHome;
		private string _dataDirectory;
		private string _javaHome;
		private string _configsDirectory;
		private string _zookeeperPropertiesPath;
		private string _logsDirectory;
		private string _zooKeeperLog4jPropertiesPath;

		public override void Run()
		{
			try
			{
				var runner = new JavaRunner(_javaHome);
				const string className = "org.apache.zookeeper.server.quorum.QuorumPeerMain";
				var classPathEntries = JavaRunner.GetClassPathForJarsInDirectories(_jarsHome);
				runner.RunClass(className, _zookeeperPropertiesPath, classPathEntries,
					defines: new Dictionary<string, string>
					{
						{ "log4j.configuration", "file:\"" + _zooKeeperLog4jPropertiesPath + "\"" }
					});
			}
			catch (Exception ex)
			{
				UploadExceptionToBlob(ex);
				throw;
			}
		}

		private void UploadExceptionToBlob(Exception ex)
		{
			var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"));
			var container = storageAccount
					.CreateCloudBlobClient()
					.GetContainerReference("logs");
			container.CreateIfNotExists();
			container
					.GetBlockBlobReference("Exception from " + RoleEnvironment.CurrentRoleInstance.Id + " on " + DateTime.Now)
					.UploadText(ex.ToString());
		}

		public override bool OnStart()
		{
			try
			{
				DiscoverDirectories();
				ExtractJars();
				JavaInstaller.ExtractJdk(_javaInstallHome);
				WriteZookeeperServerConfigFile(); // TODO: Handle role environment changes to rewrite the file and restart the server.
				WriteZookeeperLog4jFile();
				return base.OnStart();
			}
			catch (Exception ex)
			{
				UploadExceptionToBlob(ex);
				throw;
			}
		}

		private void DiscoverDirectories()
		{
			var installResource = RoleEnvironment.GetLocalResource("InstallDir");
			_javaInstallHome = Path.Combine(installResource.RootPath, "Java");
			_javaHome = Path.Combine(_javaInstallHome, "java");
			_jarsHome = Path.Combine(installResource.RootPath, "Jars");
			var dataResource = RoleEnvironment.GetLocalResource("DataDir");
			_dataDirectory = Path.Combine(dataResource.RootPath, "Data");
			Directory.CreateDirectory(_dataDirectory);
			_configsDirectory = Path.Combine(dataResource.RootPath, "Config");
			Directory.CreateDirectory(_configsDirectory);
			_zookeeperPropertiesPath = Path.Combine(_configsDirectory, "zookeeper.properties");
			_logsDirectory = Path.Combine(dataResource.RootPath, "Logs");
			Directory.CreateDirectory(_logsDirectory);
			_zooKeeperLog4jPropertiesPath = Path.Combine(_configsDirectory, "log4j.properties");
		}

		private void WriteZookeeperServerConfigFile()
		{
			var config = new ZookeeperConfig(_dataDirectory);
			config.ToPropertiesFile().WriteToFile(_zookeeperPropertiesPath);
		}

		private void WriteZookeeperLog4jFile()
		{
			var config = KafkaLog4jConfigFactory.CreateConfig(_logsDirectory);
			config.ToPropertiesFile().WriteToFile(_zooKeeperLog4jPropertiesPath);
		}

		private void ExtractJars()
		{
			using (var rawStream = typeof(WorkerRole).Assembly.GetManifestResourceStream("Zookeeper.Resources.Jars.zip"))
			using (var archive = new ZipArchive(rawStream))
			{
				archive.ExtractToDirectory(_jarsHome);
			}
		}
	}
}
