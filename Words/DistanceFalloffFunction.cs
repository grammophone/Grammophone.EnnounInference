using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grammophone.EnnounInference.Words
{
	/// <summary>
	/// Contract for weight falloff funtions taking the edit distance as a parameter, to be used
	/// with <see cref="WordClassifierBank.AnalogiesScoreOptions.DistanceFalloffFunction"/> property.
	/// </summary>
	[Serializable]
	public abstract class DistanceFalloffFunction
	{
		/// <summary>
		/// Compute the weight falloff as a function of the edit distance.
		/// </summary>
		/// <param name="editDistance">The edit distance of a word compared to another.</param>
		/// <returns>Returns the weight as a function of the <paramref name="editDistance"/>.</returns>
		public abstract double Compute(double editDistance);
	}
}
