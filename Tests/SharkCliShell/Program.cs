﻿using Microsoft.Experimental.Azure.Shark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharkCliShell
{
	class Program
	{
		static void Main(string[] args)
		{
			var runner = CreateRunner(args[0], args[1], args[2]);
			runner.RunSharkCli().WaitForExit();
		}

		private static SharkRunner CreateRunner(string sharkRoot, string sparkRoot, string javaHome)
		{
			var config = new SharkConfig(
				serverPort: 9444,
				metastoreUris: "thrift://localhost:9083",
				sparkHome: sparkRoot,
				sparkMaster: "spark://localhost:7234");
			var runner = new SharkRunner(
				sharkHome: sharkRoot,
				javaHome: javaHome,
				config: config);
			return runner;
		}
	}
}
