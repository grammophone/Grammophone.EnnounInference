using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference
{
	/// <summary>
	/// Describes the fit of a classifier against a validation set.
	/// </summary>
	/// <typeparam name="O">The type of the options used to train the classifier.</typeparam>
	public class Fit<O> : IComparable<Fit<O>>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="validationScore">The validation score against the given data.</param>
		/// <param name="trainingOptions">The training options used to train the classifier.</param>
		public Fit(double validationScore, O trainingOptions)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");

			this.ValidationScore = validationScore;
			this.TrainingOptions = trainingOptions;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The validation score against the given data.
		/// </summary>
		public double ValidationScore { get; private set; }

		/// <summary>
		/// The training options used to train the classifier.
		/// </summary>
		public O TrainingOptions { get; private set; }

		#endregion

		#region IComparable<Fit<O,C>> Members

		/// <summary>
		/// Compares against another <see cref="Fit{O}"/> with respect 
		/// to <see cref="ValidationScore"/>. If the other object is null then this object is considered greater.
		/// </summary>
		/// <param name="other">The other fit to compare.</param>
		/// <returns>
		/// Returns -1, 0, 1 if this <see cref="ValidationScore"/> is less, equal or greater compared to the <paramref name="other"/>.
		/// </returns>
		public int CompareTo(Fit<O> other)
		{
			if (other == null) return 1;

			return this.ValidationScore.CompareTo(other.ValidationScore);
		}

		#endregion
	}
}
