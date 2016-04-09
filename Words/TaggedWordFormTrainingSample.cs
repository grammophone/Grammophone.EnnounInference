using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel;
using Grammophone.LanguageModel.Provision.EditCommands;

namespace Grammophone.EnnounInference.Words
{
	/// <summary>
	/// Training sample for <see cref="WordClassifier"/>.
	/// </summary>
	[Serializable]
	public struct TaggedWordFormTrainingSample : IEquatable<TaggedWordFormTrainingSample>
	{
		#region Private fields

		private SyllabicWord word;

		private CommandSequenceClass commandSequenceClass;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="word">The syllables of the word.</param>
		/// <param name="commandSequenceClass">The class of the word.</param>
		public TaggedWordFormTrainingSample(SyllabicWord word, CommandSequenceClass commandSequenceClass)
		{
			if (word == null) throw new ArgumentNullException("word");
			if (commandSequenceClass == null) throw new ArgumentNullException("commandSequenceClass");

			this.word = word;
			this.commandSequenceClass = commandSequenceClass;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The syllables of the word.
		/// </summary>
		public SyllabicWord WordSyllables 
		{ 
			get
			{
				return word;
			}
		}

		/// <summary>
		/// The class of the word.
		/// </summary>
		public CommandSequenceClass Class 
		{ 
			get
			{
				return commandSequenceClass;
			}
		}

		#endregion

		#region IEquatable<TaggedWordFormTrainingSample> Members

		/// <summary>
		/// Returns true if the <paramref name="other"/>'s
		/// <see cref="Class"/> and <see cref="WordSyllables"/>
		/// properties are equal.
		/// </summary>
		public bool Equals(TaggedWordFormTrainingSample other)
		{
			if (this.Class == null)
			{
				return other.Class == null;
			}

			if (this.WordSyllables == null)
			{
				return other.WordSyllables == null;
			}

			return this.Class.Equals(other.Class) 
				&& this.WordSyllables.Equals(other.WordSyllables);
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Returns true if the <paramref name="obj"/> is also <see cref="TaggedWordFormTrainingSample"/>
		/// and its <see cref="Class"/> and <see cref="WordSyllables"/>
		/// properties are equal.
		/// </summary>
		public override bool Equals(object obj)
		{
			return this.Equals((TaggedWordFormTrainingSample)obj);
		}

		/// <summary>
		/// Returns a hash code depending on <see cref="Class"/> and <see cref="WordSyllables"/>
		/// properties.
		/// </summary>
		public override int GetHashCode()
		{
			int value = 0;

			if (this.WordSyllables != null) value += this.WordSyllables.GetHashCode();

			if (this.Class != null) value += 23 * this.Class.GetHashCode();

			return value;
		}

		#endregion
	}
}
