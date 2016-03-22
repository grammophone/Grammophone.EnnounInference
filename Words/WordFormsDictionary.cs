using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.Indexing;
using Gramma.GenericContentModel;
using Gramma.LanguageModel;
using System.Diagnostics;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// A dictionary of untagged word forms, stored as syllables.
	/// </summary>
	[Serializable]
	public class WordFormsDictionary : InferenceResourceItem
	{
		#region Auxilliary classes

		/// <summary>
		/// Result from a query via <see cref="GetNeighbours"/> method.
		/// </summary>
		public struct SearchResult
		{
			#region Private fields

			private SyllabicWord word;

			private double editDistance;

			#endregion

			#region Construction

			internal SearchResult(SyllabicWord word, double editDistance)
			{
				if (word == null) throw new ArgumentNullException("word");

				this.word = word;
				this.editDistance = editDistance;
			}

			#endregion

			#region Public properties

			/// <summary>
			/// The found word.
			/// </summary>
			public SyllabicWord Word
			{
				get
				{
					return word;
				}
			}

			/// <summary>
			/// The edit distance of the found word from the query word.
			/// </summary>
			public double EditDistance
			{
				get
				{
					return editDistance;
				}
			}

			#endregion
		}

		#endregion

		#region Private fields

		private WordTree<string, double, int> tree;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		internal WordFormsDictionary(InferenceResource inferenceResource, IEnumerable<SyllabicWord> words)
			: base(inferenceResource)
		{
			if (words == null) throw new ArgumentNullException("words");

			Build(words);
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the neighbours of a <paramref name="word"/> with edit distance up to <paramref name="maxEditDistance"/>.
		/// </summary>
		/// <param name="word">The syllables of the word.</param>
		/// <param name="maxEditDistance">The maximum edit distance of the seartch results.</param>
		/// <returns>
		/// Returns an collection of <see cref="SearchResult"/> items.
		/// </returns>
		public IReadOnlySequence<SearchResult> GetNeighbours(SyllabicWord word, double maxEditDistance)
		{
			if (word == null) throw new ArgumentNullException("word");

			var syllabizer = this.InferenceResource.LanguageProvider.Syllabizer;

			var treeSearchResults = tree.ApproximateSearch(
				word.ToArray(), 
				maxEditDistance, 
				(word1, word2) => syllabizer.GetDistance(word1, word2).Cost
				);

			var searchResults = from result in treeSearchResults
													select new SearchResult(new SyllabicWord(result.Match), result.EditDistance);

			return new ReadOnlySequence<SearchResult>(searchResults);
		}

		#endregion

		#region Internal methods

		internal void Build(IEnumerable<SyllabicWord> words)
		{
			if (words == null) throw new ArgumentNullException("words");

			tree = new WordTree<string, double, int>();

			Trace.WriteLine("Building word forms dictionary.");

			int count = 0;

			foreach (var word in words)
			{
				tree.AddWord(word.ToArray(), 1.0);

				count++;
			}

			Trace.WriteLine(String.Format("Added {0} word forms in the word forms dictionary.", count));
		}

		#endregion

	}
}
