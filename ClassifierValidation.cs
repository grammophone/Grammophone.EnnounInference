using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grammophone.EnnounInference
{
	/// <summary>
	/// Information about a classifier and its validation performance.
	/// </summary>
	/// <typeparam name="C">The type of the classifier.</typeparam>
	/// <typeparam name="O">The type of the options used for training the classifier.</typeparam>
	public class ClassifierValidation<C, O>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="classifier">The classifier.</param>
		/// <param name="fit">The validation fitness.</param>
		public ClassifierValidation(C classifier, Fit<O> fit)
		{
			if (classifier == null) throw new ArgumentNullException("classifier");
			if (fit == null) throw new ArgumentNullException("fit");

			this.Classifier = classifier;
			this.Fit = fit;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The classifier.
		/// </summary>
		public C Classifier { get; private set; }

		/// <summary>
		/// The validation fitness.
		/// </summary>
		public Fit<O> Fit { get; private set; }

		#endregion
	}
}
