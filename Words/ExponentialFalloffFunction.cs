using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// Returns a falloff function with formula f(editDistance) = exp(-λ * editDistance) to be used 
	/// with <see cref="WordClassifierBank.AnalogiesScoreOptions.DistanceFalloffFunction"/> property.
	/// </summary>
	[Serializable]
	public class ExponentialFalloffFunction : DistanceFalloffFunction
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="λ">
		/// The falloff rate in 
		/// <c>f(editDistance) = exp(-λ * editDistance)</c>.
		/// </param>
		public ExponentialFalloffFunction(double λ)
		{
			if (λ < 0.0) throw new ArgumentException("λ must not be negative.", "λ");

			this.λ = λ;
		}

		/// <summary>
		/// The falloff rate in 
		/// <c>f(editDistance) = exp(-λ * editDistance)</c>.
		/// </summary>
		public double λ { get; private set; }

		/// <summary>
		/// Computes
		/// <c>f(editDistance) = exp(-λ * editDistance)</c>.
		/// </summary>
		public override double Compute(double editDistance)
		{
			return Math.Exp(-λ * editDistance);
		}
	}

}
