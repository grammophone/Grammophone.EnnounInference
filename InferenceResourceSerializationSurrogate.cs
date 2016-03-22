using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Gramma.Inference
{
	/// <summary>
	/// Serialization surrogate for <see cref="InferenceResource"/>. 
	/// Maps every <see cref="InferenceResource"/> deserialized instance to a single given instance.
	/// </summary>
	public class InferenceResourceSerializationSurrogate : ISerializationSurrogate
	{
		private InferenceResource inferenceResource;

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The instance where all <see cref="InferenceResource"/> deserialized instances map to.</param>
		public InferenceResourceSerializationSurrogate(InferenceResource inferenceResource)
		{
			if (inferenceResource == null) throw new ArgumentNullException("inferenceResource");

			this.inferenceResource = inferenceResource;
		}

		/// <summary>
		/// Records nothing as the instance is implied.
		/// </summary>
		public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
		{
			// Do nothing. The instance is implied. 
		}

		/// <summary>
		/// If the <paramref name="obj"/> is not null, return the implied instance, else return null.
		/// </summary>
		public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
		{
			if (obj != null)
				return inferenceResource;
			else
				return null;
		}
	}
}
