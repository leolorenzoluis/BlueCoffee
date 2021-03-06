﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Microsoft.Experimental.Azure.Storm
{
	/// <summary>
	/// Configuration for a Storm node.
	/// </summary>
	public sealed class StormConfig
	{
		private readonly int _maxNodeMemoryMb;
		private readonly string _nimbusHost;
		private readonly ImmutableList<string> _zooKeeperServers;
		private readonly ImmutableList<string> _drpcServers;
		private readonly int _zooKeeperPort;
		private readonly string _stormLocalDirectory;

		/// <summary>
		/// Creates a new configuration.
		/// </summary>
		/// <param name="nimbusHost">The host where Nimbus is running.</param>
		/// <param name="zooKeeperServers">The names of ZooKeeper hosts.</param>
		/// <param name="zooKeeperPort">The port ZooKeeper nodes are listening on.</param>
		/// <param name="drpcServers">The list of DRPC servers available to use (defaults to null/empty).</param>
		/// <param name="maxNodeMemoryMb">Maximum amount of memory used by the Storm node.</param>
		/// <param name="stormLocalDirectory">storm.local.dir, where Storm stores its data.</param>
		public StormConfig(string nimbusHost,
				IEnumerable<string> zooKeeperServers, int zooKeeperPort = 2181,
				IEnumerable<string> drpcServers = null,
				int maxNodeMemoryMb = 2048, string stormLocalDirectory = "storm-local")
		{
			_nimbusHost = nimbusHost;
			_zooKeeperServers = zooKeeperServers.ToImmutableList();
			_zooKeeperPort = zooKeeperPort;
			_drpcServers = (drpcServers ?? Enumerable.Empty<string>()).ToImmutableList();
			_maxNodeMemoryMb = maxNodeMemoryMb;
			_stormLocalDirectory = stormLocalDirectory;
		}

		/// <summary>
		/// The host where Nimbus is running.
		/// </summary>
		public string NimbusHost { get { return _nimbusHost; } }

		/// <summary>
		/// The names of the ZooKeeper hosts.
		/// </summary>
		public ImmutableList<string> ZooKeeperServers { get { return _zooKeeperServers; } }

		/// <summary>
		/// The port ZooKeeper nodes are listening on.
		/// </summary>
		public int ZooKeeperPort { get { return _zooKeeperPort; } }

		/// <summary>
		/// The names of the DRPC server hosts.
		/// </summary>
		public ImmutableList<string> DrpcServers { get { return _drpcServers; } }

		/// <summary>
		/// Maximum amount of memory used by the Storm node.
		/// </summary>
		public int MaxNodeMemoryMb { get { return _maxNodeMemoryMb; } }

		/// <summary>
		/// storm.local.dir, where Storm stores its data.
		/// </summary>
		public string StormLocalDirectory { get { return _stormLocalDirectory; } }

		/// <summary>
		/// Write out this configuration to a YAML file for Storm.
		/// </summary>
		/// <param name="writer">The writer for the file.</param>
		public void WriteToYamlFile(TextWriter writer)
		{
			var serializer = new Serializer();
			serializer.Serialize(writer, new Dictionary<string, object>()
			{
				{ "storm.zookeeper.servers", _zooKeeperServers.ToArray() },
				{ "nimbus.host", _nimbusHost },
				{ "storm.local.dir", _stormLocalDirectory.Replace('\\', '/') },
				{ "drpc.servers", _drpcServers.ToArray() },
			});
		}
	}
}
