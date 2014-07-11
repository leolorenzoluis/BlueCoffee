﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Experimental.Azure.JavaPlatform.Log4j
{
	public abstract class LoggerDefinition
	{
		private readonly Log4jTraceLevel _level;
		private readonly ImmutableList<AppenderDefinition> _appenders;

		protected LoggerDefinition(Log4jTraceLevel level, IEnumerable<AppenderDefinition> appenders)
		{
			_level = level;
			_appenders = appenders.ToImmutableList();
		}

		public Log4jTraceLevel Level { get { return _level; } }
		public ImmutableList<AppenderDefinition> Appenders { get { return _appenders; } }

		protected string DefinitionLine
		{
			get
			{
				return String.Join(",",
					new[] { _level.ToString() }
					.Concat(_appenders.Select(a => a.Name)));
			}
		}

		public abstract ImmutableDictionary<string, string> FullLog4jProperties { get; }
	}
}
