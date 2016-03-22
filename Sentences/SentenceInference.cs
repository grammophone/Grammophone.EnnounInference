using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.GenericContentModel;

namespace Gramma.Inference.Sentences
{
	/// <summary>
	/// The result of inference upon a sentence, including probability.
	/// </summary>
	public class SentenceInference
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="lemmaInferences">
		/// The inferred components of the sentence, 
		/// or null if the sequence of the given words is estimated as impossible.
		/// </param>
		/// <param name="probability">The probability of this inderence, according to the sentence model.</param>
		public SentenceInference(IReadOnlySequence<LemmaInference> lemmaInferences, double probability)
		{
			if (probability < 0.0 || probability > 1.0)
				throw new ArgumentException("Probability should be between 0 and 1.", "probability");

			this.LemmaInferences = lemmaInferences;
			this.Probability = probability;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The inferred components of the sentence
		/// or null if the sequence of the given words is estimated as impossible.
		/// </summary>
		public IReadOnlySequence<LemmaInference> LemmaInferences { get; private set; }

		/// <summary>
		/// The probability of this inderence, according to the sentence model.
		/// </summary>
		public double Probability { get; set; }

		#endregion
	}
}
