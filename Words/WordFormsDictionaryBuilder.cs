using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel.Provision;
using Gramma.LanguageModel.TrainingSources;
using Gramma.Inference.Configuration;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// A builder of <see cref="WordFormsDictionary"/>.
	/// </summary>
	internal class WordFormsDictionaryBuilder : InferenceResourceItem
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		internal WordFormsDictionaryBuilder(InferenceResource inferenceResource)
			: base(inferenceResource)
		{

		}

		#endregion

		#region Public methods

		/// <summary>
		/// Build a <see cref="WordFormsDictionary"/> from the contents of the given training set
		/// implied from the <see cref="ReadOnlyLanguageFacet.LanguageProvider"/> of the <see cref="InferenceResourceItem.InferenceResource"/>
		/// or return null if not defined.
		/// </summary>
		/// <returns>Returns the <see cref="WordFormsDictionary"/> or null of no untagged word training sets are defined.</returns>
		/// <exception cref="SetupException">
		/// When no <see cref="TrainingSet"/> is defined for the <see cref="ReadOnlyLanguageFacet.LanguageProvider"/>
		/// of the <see cref="InferenceResource"/>.
		/// </exception>
		public WordFormsDictionary Build()
		{
			var trainingSet = GetTrainingSet();

			if (trainingSet.UntaggedWordTrainingSources.Count == 0) return null;

			var compositeTrainingSource = new CompositeTrainingSource<string>(trainingSet.UntaggedWordTrainingSources, this.InferenceResource.LanguageProvider);

			return Build(compositeTrainingSource);
		}

		/// <summary>
		/// Build a <see cref="WordFormsDictionary"/> from the contents of the given training set.
		/// </summary>
		/// <param name="trainingSource">The training set.</param>
		/// <returns>Returns the <see cref="WordFormsDictionary"/>.</returns>
		public WordFormsDictionary Build(TrainingSource<string> trainingSource)
		{
			if (trainingSource == null) throw new ArgumentNullException("trainingSource");

			using (trainingSource)
			{
				trainingSource.Open();

				var syllabizer = this.InferenceResource.LanguageProvider.Syllabizer;

				var wordsSyllables = from word in trainingSource.GetData()
														 select syllabizer.Segment(word);

				return new WordFormsDictionary(this.InferenceResource, wordsSyllables);
			}
		}

		#endregion
	}
}
