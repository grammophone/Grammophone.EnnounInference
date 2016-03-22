using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference.Sentences
{
	/// <summary>
	/// Discriminates how a word is scored by a its <see cref="Words.ScoreBank"/>. 
	/// </summary>
	[Serializable]
	public enum WordScoringPolicy
	{
		/// <summary>
		/// If the word has features in the dictionary, return the scores of these,
		/// else return the scores by the classifiers.
		/// </summary>
		Prioritized,

		/// <summary>
		/// Return the mixed scores from both the dictionary and the classifiers,
		/// unless the queried tag is of 'singular' type. In the latter case,
		/// return scores from the dictionary only.
		/// </summary>
		Mixed,

		/// <summary>
		/// If the word has features taken care by the classifiers, return the scores of these,
		/// else return the scores from the dictionary.
		/// </summary>
		Proportional
	}
}
