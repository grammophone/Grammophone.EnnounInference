using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel.Provision.EditCommands;
using Gramma.GenericContentModel;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Represents a word's score value for a <see cref="WordFeature"/>.
	/// </summary>
	[Serializable]
	public class Score : IComparable<Score>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="feature">The word feature being scored.</param>
		/// <param name="scoreValue">The membership scoreValue of the word. Positive values indicate an estimation of membership.</param>
		internal Score(WordFeature feature, double scoreValue)
		{
			if (feature == null) throw new ArgumentNullException("feature");

			this.Feature = feature;
			this.ScoreValue = scoreValue;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The word feature being scored.
		/// </summary>
		public WordFeature Feature { get; private set; }

		/// <summary>
		/// The membership scoreValue of the word. Positive values indicate an estimation of membership.
		/// </summary>
		public double ScoreValue { get; internal set; }

		#endregion

		#region IComparable<Score> Members

		/// <summary>
		/// Compares this score to another score based on their <see cref="ScoreValue"/> property.
		/// </summary>
		/// <param name="other">The other score to compare.</param>
		/// <returns>
		/// Returns 1, 0, or -1 if this score's <see cref="ScoreValue"/> is 
		/// correspondingly greater, equal or less than the <paramref name="other"/> score's <see cref="ScoreValue"/>.
		/// </returns>
		public int CompareTo(Score other)
		{
			if (other == null) throw new ArgumentNullException("other");

			return this.ScoreValue.CompareTo(other.ScoreValue);
		}

		#endregion

		#region Operators

		/// <summary>
		/// Returns a score with a <see cref="ScoreValue"/> multiplied by <paramref name="factor"/>.
		/// </summary>
		public static Score operator *(Score score, double factor)
		{
			if (score == null) throw new ArgumentNullException("score");
			return new Score(score.Feature, score.ScoreValue * factor);
		}

		/// <summary>
		/// Returns a score with a <see cref="ScoreValue"/> multiplied by <paramref name="factor"/>.
		/// </summary>
		public static Score operator *(double factor, Score score)
		{
			if (score == null) throw new ArgumentNullException("score");
			return new Score(score.Feature, score.ScoreValue * factor);
		}

		#endregion
	}
}
