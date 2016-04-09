using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel;
using Grammophone.LanguageModel.Provision;

namespace Grammophone.EnnounInference.Configuration
{
	/// <summary>
	/// Exception for errors in the defined <see cref="Setup"/>.
	/// </summary>
	[Serializable]
	public class SetupException : InferenceException
	{
		/// <summary>
		/// Create.
		/// </summary>
		public SetupException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		public SetupException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected SetupException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
