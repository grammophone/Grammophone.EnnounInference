using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel.Provision;
using Gramma.LanguageModel.Grammar;

namespace Gramma.Inference.Sentences
{
	/// <summary>
	/// An inferred tag-lemma pair for a word.
	/// </summary>
	public class LemmaInference
	{
		#region Private fields

		private string form;

		private Tag tag;

		private string lemma;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="form">
		/// The word's form.
		/// </param>
		/// <param name="tag">
		/// The inferred tag of the word.
		/// </param>
		/// <param name="lemma">
		/// The inferrred lemma of the word. Might be normalized (for example, capitalized or missing accents), depending on <see cref="LanguageProvider"/>.
		/// </param>
		public LemmaInference(string form, Tag tag, string lemma)
		{
			if (form == null) throw new ArgumentNullException("form");
			if (tag == null) throw new ArgumentNullException("tag");
			if (lemma == null) throw new ArgumentNullException("lemma");

			this.form = form;
			this.tag = tag;
			this.lemma = lemma;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The word form.
		/// </summary>
		public string Form
		{
			get { return form; }
		}

		/// <summary>
		/// The inferred tag of the word.
		/// </summary>
		public Tag Tag
		{ 
			get { return tag; }
		}

		/// <summary>
		/// The inferrred lemma of the word. Might be normalized (for example, capitalized or missing accents), 
		/// depending on <see cref="LanguageProvider"/>.
		/// </summary>
		public string Lemma 
		{
			get { return lemma; } 
		}

		#endregion
	}
}
