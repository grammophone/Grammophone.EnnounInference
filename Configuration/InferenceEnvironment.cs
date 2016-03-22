using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xaml;
using Gramma.LanguageModel;
using Gramma.LanguageModel.Provision;
using Gramma.LanguageModel.TrainingSources;
using Gramma.Inference.Extensions;
using System.Threading.Tasks;
using Gramma.Configuration;

namespace Gramma.Inference.Configuration
{
	/// <summary>
	/// The inference environment.
	/// </summary>
	public static class InferenceEnvironment
	{
		#region Private fields

		private static XamlConfiguration<Setup> xamlConfiguration =
			new XamlConfiguration<Setup>("inferenceSection");

		private static InferenceResources inferenceResources = new InferenceResources();

		#endregion

		#region Public properties

		/// <summary>
		/// The <see cref="Setup"/> declaration of the inference system,
		/// as specified in the XAML file residing at <see cref="XamlSettingsSection.SettingsXamlPath"/>.
		/// </summary>
		/// <exception cref="SetupException">
		/// When there is a logical inconsistency in the <see cref="Setup"/> object.
		/// </exception>
		/// <exception cref="System.IO.FileNotFoundException">
		/// When the XAML file was not found at <see cref="XamlSettingsSection.SettingsXamlPath"/>.
		/// </exception>
		/// <exception cref="ConfigurationException">
		/// When the object in the XAML file at <see cref="XamlSettingsSection.SettingsXamlPath"/> is not a <see cref="Setup"/> object.
		/// </exception>
		public static Setup Setup
		{
			get
			{
				return xamlConfiguration.Settings;
			}
		}

		/// <summary>
		/// The collection of current <see cref="InferenceResource"/> items, indexed by
		/// their <see cref="ReadOnlyLanguageFacet.LanguageProvider"/> property.
		/// </summary>
		public static InferenceResources InferenceResources
		{
			get
			{
				return inferenceResources;
			}
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the inference resource that corresponds to the <paramref name="languageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The language provider.</param>
		/// <returns>
		/// Returns the corresponding <see cref="InferenceResource"/>, if it exists, 
		/// else throws a <see cref="SetupException"/>.
		/// </returns>
		/// <exception cref="SetupException">
		/// When no <see cref="InferenceResource"/> exists in <see cref="InferenceEnvironment"/> corresponding to
		/// the <paramref name="languageProvider"/>.
		/// </exception>
		public static InferenceResource GetInferenceResource(LanguageProvider languageProvider)
		{
			if (languageProvider == null) throw new ArgumentNullException("languageProvider");

			if (!InferenceEnvironment.InferenceResources.ContainsKey(languageProvider))
				throw new SetupException("No inference resource is defined for the specified language provider.");

			return InferenceEnvironment.InferenceResources[languageProvider];
		}

		/// <summary>
		/// Get the <see cref="LanguageProvider"/> which is defined in the current setup
		/// having the given <paramref name="languageKey"/>.
		/// </summary>
		/// <param name="languageKey">The <see cref="LanguageProvider.LanguageKey"/>.</param>
		/// <returns>
		/// Returns the corresponding <see cref="LanguageProvider"/> found 
		/// or throws a <see cref="SetupException"/>.
		/// </returns>
		/// <exception cref="SetupException">
		/// When a <see cref="LanguageProvider"/> has not been defined in <see cref="InferenceEnvironment.Setup"/>
		/// having the corresponding <see cref="LanguageProvider.LanguageKey"/>.
		/// </exception>
		public static LanguageProvider GetLanguageProvider(string languageKey)
		{
			if (languageKey == null) throw new ArgumentNullException("languageKey");

			if (!InferenceEnvironment.Setup.LanguageProviders.ContainsKey(languageKey))
				throw new SetupException("No language provider is defined having this language key.");

			return InferenceEnvironment.Setup.LanguageProviders[languageKey];
		}

		/// <summary>
		/// Load an <see cref="InferenceResource"/> from the <see cref="InferenceResourceProvider"/> specified by a
		/// given <see cref="LanguageProvider"/>
		/// and register it under <see cref="InferenceEnvironment.InferenceResources"/>,
		/// replacing the preexisting item if it exists for the same <see cref="LanguageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The <see cref="LanguageProvider"/> to be associated with the loaded <see cref="InferenceResource"/>.</param>
		/// <returns>
		/// Returns a task whose result is the <see cref="InferenceResource"/> loaded or null
		/// if the <see cref="InferenceResourceProvider"/> for the <paramref name="languageProvider"/>
		/// does not exist.
		/// </returns>
		public static Task<InferenceResource> LoadInferenceResourceAsync(LanguageProvider languageProvider)
		{
			if (languageProvider == null) throw new ArgumentNullException(nameof(languageProvider));

			if (!Setup.InferenceResourceProviders.ContainsKey(languageProvider))
				return Task.FromResult<InferenceResource>(null);

			var provider = Setup.InferenceResourceProviders[languageProvider];

			string filename = provider.Path;

			return LoadInferenceResourceAsync(languageProvider, filename);
		}

		/// <summary>
		/// Load an <see cref="InferenceResource"/> from file and register it under <see cref="InferenceEnvironment.InferenceResources"/>,
		/// replacing preexisting item if it exists for the same <see cref="LanguageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The <see cref="LanguageProvider"/> to be associated with the loaded <see cref="InferenceResource"/>.</param>
		/// <param name="filename">The filename of the <see cref="InferenceResource"/> saved data.</param>
		/// <returns>
		/// Returns a task whose result is the <see cref="InferenceResource"/> loaded.
		/// </returns>
		public static Task<InferenceResource> LoadInferenceResourceAsync(LanguageProvider languageProvider, string filename)
		{
			if (languageProvider == null) throw new ArgumentNullException(nameof(languageProvider));
			if (filename == null) throw new ArgumentNullException(nameof(filename));

			var task = Task.Factory.StartNew(
				() => LoadInferenceResource(languageProvider, filename),
				TaskCreationOptions.LongRunning);

			task.AppendTraceExceptionHandler();

			return task;
		}

		/// <summary>
		/// Load an <see cref="InferenceResource"/> from file and register it under <see cref="InferenceEnvironment.InferenceResources"/>,
		/// replacing preexisting item if it exists for the same <see cref="LanguageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The <see cref="LanguageProvider"/> to be associated with the loaded <see cref="InferenceResource"/>.</param>
		/// <param name="filename">The filename of the <see cref="InferenceResource"/> saved data.</param>
		/// <returns>Returns the <see cref="InferenceResource"/> loaded.</returns>
		public static InferenceResource LoadInferenceResource(LanguageProvider languageProvider, string filename)
		{
			if (languageProvider == null) throw new ArgumentNullException("languageProvider");
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
			{
				return LoadInferenceResource(languageProvider, stream);
			}
		}

		/// <summary>
		/// Load an <see cref="InferenceResource"/> from stream and register it under <see cref="InferenceEnvironment.InferenceResources"/>,
		/// replacing preexisting item if it exists for the same <see cref="LanguageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The <see cref="LanguageProvider"/> to be associated with the loaded <see cref="InferenceResource"/>.</param>
		/// <param name="stream">The stream of the <see cref="InferenceResource"/> saved data.</param>
		/// <returns>Returns the <see cref="InferenceResource"/> loaded.</returns>
		public static InferenceResource LoadInferenceResource(LanguageProvider languageProvider, Stream stream)
		{
			if (languageProvider == null) throw new ArgumentNullException("languageProvider");
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = InferenceResource.GetFormatter(languageProvider);

			var inferenceResource = (InferenceResource)formatter.Deserialize(stream);

			inferenceResource.LanguageProvider = languageProvider;

			// Replace any existing InferenceResource associated with the same LanguageProvider.

			if (InferenceResources.ContainsKey(languageProvider))
			{
				InferenceResources.RemoveKey(languageProvider);
			}

			InferenceResources.Add(inferenceResource);

			return inferenceResource;
		}

		/// <summary>
		/// Return a task for getting the <see cref="InferenceResources"/> implied by the <see cref="InferenceResourceProviders"/>
		/// defined in this environment. Upon task successful completion, the <see cref="InferenceEnvironment.InferenceResources"/>
		/// will be set to the loaded inference resources.
		/// The task's exceptions need not be handled, though they can be inspected and handled the usual way.
		/// </summary>
		/// <returns>Returns a task which can be waited, continued, handled the usual way.</returns>
		/// <remarks>
		/// The <see cref="InferenceResources"/> are loaded only once upon the first the call, subsequent calls
		/// return short tasks with the same cached result and exceptions as the first call.
		/// </remarks>
		public static Task<InferenceResources> LoadInferenceResourcesAsync()
		{
			var task = Task.Factory.StartNew<InferenceResources>(() =>
				{
					var loadedInferenceResources = LoadInferenceResources();

					inferenceResources = loadedInferenceResources;

					return loadedInferenceResources;
				},
				TaskCreationOptions.LongRunning);

			task.AppendTraceExceptionHandler();

			return task;
		}

		#endregion

		#region Private methods

		private static InferenceResources LoadInferenceResources()
		{
			var inferenceResources = new InferenceResources();

			foreach (var inferenceResourceProvider in InferenceEnvironment.Setup.InferenceResourceProviders)
			{
				inferenceResources.Add(inferenceResourceProvider.Load());
			}

			return inferenceResources;
		}

		#endregion
	}
}
