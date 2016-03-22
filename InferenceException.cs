using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Inference
{
	/// <summary>
	/// Exception for the inference system.
	/// </summary>
	[Serializable]
	public class InferenceException : Exception
	{
		/// <summary>
		/// Create.
		/// </summary>
		public InferenceException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		public InferenceException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected InferenceException(
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
