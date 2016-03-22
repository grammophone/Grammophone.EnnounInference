using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel;
using Gramma.LanguageModel.Provision;
using Gramma.LanguageModel.TrainingSources;

namespace Gramma.Inference.Configuration
{
	/// <summary>
	/// Intended to be the root of the XAML configuration.
	/// </summary>
	public class Setup : Gramma.Configuration.IXamlLoadListener
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public Setup()
		{
			this.LanguageProviders = new LanguageProviders();
			this.TrainingSets = new TrainingSets();
			this.ValidationSets = new ValidationSets();
			this.InferenceResourceProviders = new InferenceResourceProviders();
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The available language providers, indexed by <see cref="LanguageProvider.LanguageKey"/>.
		/// </summary>
		public LanguageProviders LanguageProviders { get; private set; }

		/// <summary>
		/// The available training sets, indexed by <see cref="LanguageProvider"/>.
		/// </summary>
		public TrainingSets TrainingSets { get; private set; }

		/// <summary>
		/// The available validation sets, indexed by <see cref="LanguageProvider"/>.
		/// </summary>
		public ValidationSets ValidationSets { get; private set; }

		/// <summary>
		/// The providers of inference resources, indexed by <see cref="LanguageProvider"/>.
		/// </summary>
		public InferenceResourceProviders InferenceResourceProviders { get; private set; }

		#endregion

		#region IXamlLoadListener implementation

		void Gramma.Configuration.IXamlLoadListener.OnPostLoad(object sender)
		{
			foreach (var trainingSet in this.TrainingSets)
			{
				if (trainingSet.LanguageProvider == null)
					throw new SetupException("The training set has no LanguageProvider specified.");

				trainingSet.AssignLanguageProviderToSources();
			}

			foreach (var validationSet in this.ValidationSets)
			{
				if (validationSet.LanguageProvider == null)
					throw new SetupException("The validation set has no LanguageProvider specified.");

				validationSet.AssignLanguageProviderToSources();
			}
		}

		#endregion
	}
}
