using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Options for training <see cref="WordClassifier"/>s.
	/// </summary>
	[Serializable]
	public class WordClassifierTrainingOptions
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public WordClassifierTrainingOptions()
		{
			this.StringKernelExponent = 1.0;
			this.IsGaussified = false;
			this.GaussianDeviation = 1.0;
			this.ClassificationMarginSlack = 10.0;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The exponent of the string length weight of each term in the string kernel.
		/// Default is 1.0.
		/// </summary>
		public double StringKernelExponent { get; set; }

		/// <summary>
		/// If true, the classifier's kernel is gaussified.
		/// Default is false.
		/// </summary>
		public bool IsGaussified { get; set; }

		/// <summary>
		/// If <see cref="IsGaussified"/> is true, this is the σ parameter of the Gaussian, else irrelevant.
		/// Default is 1.0.
		/// </summary>
		public double GaussianDeviation { get; set; }

		/// <summary>
		/// The famous C constant of soft margin SVM classifier.
		/// Default is 10.
		/// </summary>
		public double ClassificationMarginSlack { get; set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Returns a string description of all the properties.
		/// </summary>
		public override string ToString()
		{
			if (!this.IsGaussified)
			{
				return String.Format(
					"Margin slack: {0}, String kernel exponent: {1}, Is Gaussified: {2}",
					this.ClassificationMarginSlack,
					this.StringKernelExponent,
					this.IsGaussified);
			}
			else
			{
				return String.Format(
					"Margin slack: {0}, String kernel exponent: {1}, Is Gaussified: {2}, Gaussian deviation: {3}",
					this.ClassificationMarginSlack,
					this.StringKernelExponent,
					this.IsGaussified,
					this.GaussianDeviation);
			}
		}

		#endregion
	}
}
