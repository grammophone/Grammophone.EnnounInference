using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel.Provision;
using Grammophone.LanguageModel.TrainingSources;
using Grammophone.EnnounInference.Configuration;

namespace Grammophone.EnnounInference
{
	/// <summary>
	/// An item associated with an <see cref="InferenceResource"/>.
	/// </summary>
	[Serializable]
	public abstract class InferenceResourceItem
	{
		#region Private fields

		///// <summary>
		///// This should be manually set after serialization through the internal setter of 
		///// <see cref="InferenceResourceItem.InferenceResource"/> property.
		///// </summary>
		//[NonSerialized]
		[System.Runtime.Serialization.OptionalField]
		private InferenceResource inferenceResource;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The inference resource associated with this item.</param>
		public InferenceResourceItem(InferenceResource inferenceResource)
		{
			if (inferenceResource == null) throw new ArgumentNullException("inferenceResource");

			this.InferenceResource = inferenceResource;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The inference resource associated with this item.
		/// </summary>
		public InferenceResource InferenceResource 
		{
			get
			{
				return inferenceResource;
			}
			internal set
			{
				inferenceResource = value;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Get the training set that corresponds to the <see cref="LanguageProvider"/>
		/// of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </summary>
		/// <returns>
		/// Returns the corresponding <see cref="TrainingSet"/>, if it exists, 
		/// else throws a <see cref="SetupException"/>.
		/// </returns>
		/// <exception cref="SetupException">
		/// When no <see cref="TrainingSet"/> exists in <see cref="Setup"/> corresponding to
		/// the <see cref="LanguageProvider"/> of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </exception>
		protected TrainingSet GetTrainingSet()
		{
			var trainingSets = InferenceEnvironment.Setup.TrainingSets;

			var languageProvider = this.InferenceResource.LanguageProvider;

			if (!trainingSets.ContainsKey(languageProvider))
				throw new SetupException("No training set is defined for the specified language provider.");

			var trainingSet = trainingSets[languageProvider];

			return trainingSet;
		}

		/// <summary>
		/// Get the validation set that corresponds to the <see cref="LanguageProvider"/>
		/// of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </summary>
		/// <returns>
		/// Returns the corresponding <see cref="ValidationSet"/>, if it exists, 
		/// else throws a <see cref="SetupException"/>.
		/// </returns>
		/// <exception cref="SetupException">
		/// When no <see cref="ValidationSet"/> exists in <see cref="Setup"/> corresponding to
		/// the <see cref="LanguageProvider"/> of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </exception>
		protected ValidationSet GetValidationSet()
		{
			var validationSets = InferenceEnvironment.Setup.ValidationSets;

			var languageProvider = this.InferenceResource.LanguageProvider;

			if (!validationSets.ContainsKey(languageProvider))
				throw new SetupException("No validation set is defined for the specified language provider.");

			var validationSet = validationSets[languageProvider];

			return validationSet;
		}

		#endregion
	}
}
