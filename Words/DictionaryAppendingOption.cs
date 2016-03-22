using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Describes what additional scores to include in
	/// WordClassifierBank.GetScoreBank methods.
	/// </summary>
	public enum DictionaryAppendingOption
	{
		/// <summary>
		/// Append scores from the full dictionary of all known words.
		/// </summary>
		Full,

		/// <summary>
		/// Append scores from the dictionary of words whose features are not overlapped by classifiers.
		/// </summary>
		ResidualOnly
	}
}
