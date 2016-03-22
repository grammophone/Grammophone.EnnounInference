using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.GenericContentModel;
using Gramma.LanguageModel.Provision.EditCommands;
using Gramma.LanguageModel.Grammar;
using Gramma.LanguageModel;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Set of scores for a word against various 
	/// classes of type <see cref="CommandSequenceClass"/>.
	/// </summary>
	public class ScoreBank
	{
		#region Private fields

		private Lazy<IReadOnlyMultiDictionary<Tag, Score>> lazyScoresByTag;

		private Lazy<IReadOnlyMultiDictionary<Tag, Score>> lazyClassifiersScoresByTag;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="word">The word being scored.</param>
		/// <param name="classifierScores">The scores reported by neuron classifiers, sorted by feature ID.</param>
		/// <param name="dictionaryScores">The scores reported by the words dictionary, sorted by feature ID.</param>
		internal ScoreBank(SyllabicWord word, IEnumerable<Score> classifierScores, IEnumerable<Score> dictionaryScores)
		{
			if (word == null) throw new ArgumentNullException("word");
			if (classifierScores == null) throw new ArgumentNullException("classifierScores");
			if (dictionaryScores == null) throw new ArgumentNullException("dictionaryScores");

			this.Word = word;

			// Make sure that the items under a key remain in order.
			// For some reason, at least in .NET FRamework 4.0, 
			// the ReadOnlyBag<WordFeature>, which uses internally HashSet<WordFeature>,
			// keeps the items enumerable in insertion order, as long as no removal takes place.
			// But this behaviour is not documented and is prone to break when the code runs 
			// in later versions of the .NET framework.
			// So, use a ReadOnlySequence<WordFeature> instead.

			this.DictionaryScoresByTag = new ReadOnlyMultiDictionary<Tag, Score, ReadOnlySequence<Score>>
				(dictionaryScores, score => score.Feature.Class.Tag);

			this.lazyClassifiersScoresByTag = new Lazy<IReadOnlyMultiDictionary<Tag, Score>>(
				() => 
					new ReadOnlyMultiDictionary<Tag, Score>(classifierScores, score => score.Feature.Class.Tag),
				System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

			this.lazyScoresByTag = new Lazy<IReadOnlyMultiDictionary<Tag, Score>>(
				() => new ReadOnlyMultiDictionary<Tag, Score>(
					this.ClassifiersScoresByTag.SelectMany(entry => entry.Value).Concat(dictionaryScores), score => score.Feature.Class.Tag),
				System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

		}

		#endregion

		#region Public properties

		/// <summary>
		/// The syllables of the word.
		/// </summary>
		public SyllabicWord Word { get; private set; }

		/// <summary>
		/// The collection of scores for various <see cref="CommandSequenceClass"/> classes.
		/// The collection is keyed by the <see cref="WordFeature.Class"/> 
		/// in the <see cref="Score.Feature"/> of each <see cref="Score"/>.
		/// </summary>
		public IEnumerable<Score> Scores 
		{
			get
			{
				return this.ClassifiersScoresByTag.SelectMany(entry => entry.Value).Concat(this.DictionaryScoresByTag.SelectMany(entry => entry.Value));
			}
		}

		/// <summary>
		/// Scores reported by both classifiers and dictionaries, indexed by tag.
		/// </summary>
		public IReadOnlyMultiDictionary<Tag, Score> ScoresByTag
		{
			get
			{
				return lazyScoresByTag.Value;
			}
		}

		/// <summary>
		/// Scores reported by the words features dictionary, indexed by tag.
		/// </summary>
		public IReadOnlyMultiDictionary<Tag, Score> DictionaryScoresByTag { get; private set; }

		/// <summary>
		/// Scores reported by both classifiers and dictionaries, indexed by tag.
		/// </summary>
		public IReadOnlyMultiDictionary<Tag, Score> ClassifiersScoresByTag
		{
			get
			{
				return lazyClassifiersScoresByTag.Value;
			}
		}

		#endregion

		#region Public methods

		/// <summary>
		/// If the word has features in the dictionary, return the scores of these,
		/// else return the scores by the classifiers.
		/// </summary>
		public IReadOnlyBag<Score> GetPrioritizedScoresByTag(Tag tag)
		{
			if (tag == null) throw new ArgumentNullException("tag");

			if (this.DictionaryScoresByTag.Count > 0)
			{
				return this.DictionaryScoresByTag[tag];
			}
			else
			{
				return this.ClassifiersScoresByTag[tag];
			}
		}

		/// <summary>
		/// Return the mixed scores from both the dictionary and the classifiers,
		/// unless the <paramref name="tag"/> is of 'singular' type. In the latter case,
		/// return scores from the dictionary only.
		/// </summary>
		public IReadOnlyBag<Score> GetMixedScoresByTag(Tag tag)
		{
			if (tag == null) throw new ArgumentNullException("tag");

			if (tag.Type.AreTagsUnrelated)
			{
				return this.DictionaryScoresByTag[tag];
			}
			else
			{
				return this.ScoresByTag[tag];
			}
		}

		#endregion
	}
}
