using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using Gramma.GenericContentModel;
using Gramma.LanguageModel.Provision;
using Gramma.LanguageModel.Grammar;
using Gramma.LanguageModel.Grammar.SerializationSurrogates;

namespace Gramma.Inference
{
	/// <summary>
	/// A complete set of inference services for a language.
	/// </summary>
	[Serializable]
	public class InferenceResource : IKeyedElement<LanguageProvider>
	{
		#region Auxilliary classes

		/// <summary>
		/// Training options for the whole <see cref="InferenceResource"/>.
		/// Used in <see cref="Train"/> method.
		/// </summary>
		[Serializable]
		public class TrainingOptions
		{
			#region Private fields

			private Words.WordClassifierTrainingOptions wordClassifierTrainingOptions;

			private Sentences.SentenceClassifierTrainingOptions sentenceClassifierTrainingOptions;

			#endregion

			#region Construction

			/// <summary>
			/// Create.
			/// </summary>
			public TrainingOptions()
			{
				wordClassifierTrainingOptions = new Words.WordClassifierTrainingOptions();
				sentenceClassifierTrainingOptions = new Sentences.SentenceClassifierTrainingOptions();
			}

			#endregion

			#region Public properties

			/// <summary>
			/// Training options for <see cref="InferenceResource.WordClassifierBank"/>.
			/// </summary>
			public Words.WordClassifierTrainingOptions WordClassifierTrainingOptions 
			{
				get
				{
					return wordClassifierTrainingOptions;
				}
				set
				{
					if (value == null) throw new ArgumentNullException("value");
					wordClassifierTrainingOptions = value;
				}
			}

			/// <summary>
			/// Training options for <see cref="InferenceResource.SentenceClassifier"/>.
			/// </summary>
			public Sentences.SentenceClassifierTrainingOptions SentenceClassifierTrainingOptions
			{
				get
				{
					return sentenceClassifierTrainingOptions;
				}
				set
				{
					if (value == null) throw new ArgumentNullException("value");
					sentenceClassifierTrainingOptions = value;
				}
			}

			#endregion
		}

		/// <summary>
		/// Combinations of options for searching optimum training using cross-validation.
		/// Used in <see cref="OptimalTrain"/> method.
		/// </summary>
		public class TrainingOptionsGrid
		{
			#region Private fields

			private IEnumerable<Words.WordClassifierTrainingOptions> wordClassifierTrainingOptionsGrid;

			private IEnumerable<Sentences.SentenceClassifierTrainingOptions> sentenceClassifierTrainingOptionsGrid;

			#endregion

			#region Construction

			/// <summary>
			/// Create.
			/// </summary>
			public TrainingOptionsGrid()
			{
				this.wordClassifierTrainingOptionsGrid = GetDefaultWordClassifierTrainingOptionsGrid();
				this.sentenceClassifierTrainingOptionsGrid = GetDefaultSentenceClassifierTrainingOptions();
			}

			#endregion

			#region Public properties

			/// <summary>
			/// Combinations of training options for searching he optimal <see cref="Words.WordClassifierBank"/>.
			/// Must contain at least one combintation.
			/// By default, the property holds a single combination consisting of the default <see cref="Words.WordClassifierTrainingOptions"/>.
			/// </summary>
			public IEnumerable<Words.WordClassifierTrainingOptions> WordClassifierTrainingOptionsGrid
			{
				get
				{
					return wordClassifierTrainingOptionsGrid;
				}
				set
				{
					if (value == null) throw new ArgumentNullException("value");

					if (value.FirstOrDefault() == null) throw new ArgumentException("The enumeration must hold at least one element.");

					wordClassifierTrainingOptionsGrid = value;
				}
			}

			/// <summary>
			/// Combinations of training options for searching he optimal <see cref="Sentences.SentenceClassifier"/>.
			/// Must contain at least one combintation.
			/// By default, the property holds a single combination consisting of the default <see cref="Sentences.SentenceClassifierTrainingOptions"/>.
			/// </summary>
			public IEnumerable<Sentences.SentenceClassifierTrainingOptions> SentenceClassifierTrainingOptionsGrid
			{
				get
				{
					return sentenceClassifierTrainingOptionsGrid;
				}
				set
				{
					if (value == null) throw new ArgumentNullException("value");

					if (value.FirstOrDefault() == null) throw new ArgumentException("The enumeration must hold at least one element.");

					sentenceClassifierTrainingOptionsGrid = value;
				}
			}

			#endregion

			#region Private methods

			private static IEnumerable<Words.WordClassifierTrainingOptions> GetDefaultWordClassifierTrainingOptionsGrid()
			{
				yield return new Words.WordClassifierTrainingOptions();
			}

			private static IEnumerable<Sentences.SentenceClassifierTrainingOptions> GetDefaultSentenceClassifierTrainingOptions()
			{
				yield return new Sentences.SentenceClassifierTrainingOptions();
			}

			#endregion
		}

		#endregion

		#region Private fields

		[NonSerialized]
		private LanguageProvider languageProvider;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="languageProvider">The provider of the language associated with the inference services.</param>
		public InferenceResource(LanguageProvider languageProvider)
		{
			if (languageProvider == null) throw new ArgumentNullException("languageProvider");

			this.languageProvider = languageProvider;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The language provider associated with this inference resource.
		/// </summary>
		public LanguageProvider LanguageProvider 
		{
			get
			{
				return languageProvider;
			}
			internal set
			{
				languageProvider = value;
			}
		}

		/// <summary>
		/// The word classifier bank accompanying this inference resource.
		/// </summary>
		public Words.WordClassifierBank WordClassifierBank { get; internal set; }

		/// <summary>
		/// The dictionary of word forms accompanying this inference resource.
		/// </summary>
		public Words.WordFormsDictionary WordFormsDictionary { get; private set; }

		/// <summary>
		/// The sentences classifier accompanying this inference resource.
		/// </summary>
		public Sentences.SentenceClassifier SentenceClassifier { get; private set; }

		#endregion

		#region Public methods

		#region Training

		/// <summary>
		/// Train the whole inference resource with the available training sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>. This includes training 
		/// the <see cref="WordClassifierBank"/>, the <see cref="WordFormsDictionary"/>
		/// and the <see cref="SentenceClassifier"/>.
		/// </summary>
		/// <param name="trainingOptions">The training options for all systems.</param>
		/// <param name="wordDropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="wordDecimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="tagBiGramsDropout">
		/// Excludes tag bi-grams with frequency below the specified amount.
		/// </param>
		/// <param name="sentencesStride">The step size of picking samples within the sentences training set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for tagged words training. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <remarks>
		/// This is equivalent to calling consecutively <see cref="TrainWordClassifierBank"/>, 
		/// <see cref="BuildWordFormsDictionary"/>, <see cref="TrainSentences"/>.
		/// </remarks>
		public void Train(
			TrainingOptions trainingOptions, 
			double wordDropout, 
			int wordDecimation,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism = 0)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (wordDropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (wordDecimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "wordDecimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var taggedWordFormTrainer = new Words.TaggedWordFormTrainer(this);

			this.WordClassifierBank = 
				taggedWordFormTrainer.Train(trainingOptions.WordClassifierTrainingOptions, wordDropout, wordDecimation, degreeOfParallelism);

			if (trainingOptions.SentenceClassifierTrainingOptions.AnalogiesScoreOptions != null)
			{
				var wordFormsDictionaryBuilder = new Words.WordFormsDictionaryBuilder(this);

				this.WordFormsDictionary = wordFormsDictionaryBuilder.Build();
			}

			var taggedSentencesTrainer = new Sentences.TaggedSentenceTrainer(this);

			this.SentenceClassifier = 
				taggedSentencesTrainer.Train(trainingOptions.SentenceClassifierTrainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);
		}

		/// <summary>
		/// Train the <see cref="WordClassifierBank"/> of this resource with the available training sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>.
		/// </summary>
		/// <param name="trainingOptions">The training options <see cref="WordClassifierBank"/>.</param>
		/// <param name="wordDropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="wordDecimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for tagged words training. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		public void TrainWordClassifierBank(
			Words.WordClassifierTrainingOptions trainingOptions, 
			double wordDropout,
			int wordDecimation,
			int degreeOfParallelism = 0)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (wordDropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (wordDecimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "wordDecimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var taggedWordFormTrainer = new Words.TaggedWordFormTrainer(this);

			this.WordClassifierBank = taggedWordFormTrainer.Train(trainingOptions, wordDropout, wordDecimation, degreeOfParallelism);
		}

		/// <summary>
		/// Build the <see cref="WordFormsDictionary"/> of this resource with the available sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>.
		/// </summary>
		public void BuildWordFormsDictionary()
		{
			var wordFormsDictionaryBuilder = new Words.WordFormsDictionaryBuilder(this);

			this.WordFormsDictionary = wordFormsDictionaryBuilder.Build();
		}

		/// <summary>
		/// Train the <see cref="WordClassifierBank"/> of this resource with the available training sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>.
		/// The <see cref="WordClassifierBank"/> and <see cref="WordFormsDictionary"/> must have been previously loaded or trained.
		/// </summary>
		/// <param name="trainingOptions">The training options <see cref="SentenceClassifier"/>.</param>
		/// <param name="tagBiGramsDropout">
		/// Excludes tag bi-grams with frequency below the specified amount.
		/// </param>
		/// <param name="sentencesStride">The step size of picking samples within the sentences training set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		public void TrainSentences(
			Sentences.SentenceClassifierTrainingOptions trainingOptions,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism
			)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");

			if (this.WordClassifierBank == null)
			{
				throw new InferenceException("The WordClassifierBank property must have previously been trained or loaded.");
			}

			if (trainingOptions.AnalogiesScoreOptions != null)
			{
				throw new InferenceException("The WordFormsDictionary property must have previously been trained or loaded when analogies score options are present.");
			}

			var taggedSentenceTrainer = new Sentences.TaggedSentenceTrainer(this);

			this.SentenceClassifier = taggedSentenceTrainer.Train(trainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);
		}

		/// <summary>
		/// Train the whole inference resource optimally using cross-validation with the available training sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>. This includes training 
		/// the <see cref="WordClassifierBank"/>, the <see cref="WordFormsDictionary"/>
		/// and the <see cref="SentenceClassifier"/>.
		/// </summary>
		/// <param name="trainingOptionsGrid">The grid of training options combinations.</param>
		/// <param name="foldCount">The fold count used in K-fold cross-validation.</param>
		/// <param name="wordDropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="wordDecimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="tagBiGramsDropout">
		/// Excludes tag bi-grams with frequency below the specified amount.
		/// </param>
		/// <param name="sentencesStride">The step size of picking samples within the sentences training set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for tagged words training. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <remarks>
		/// Currently cross-validation for training <see cref="SentenceClassifier"/> is not supported due to computational resources constraints,
		/// thus the <see cref="TrainingOptionsGrid.SentenceClassifierTrainingOptionsGrid"/> is expected to contain a single item.
		/// This method is equivalent to calling consecutively <see cref="OptimalTrainWordClassifierBank"/>,
		/// <see cref="BuildWordFormsDictionary"/>, and invoking <see cref="TrainSentences"/> with the one and only
		/// item in <see cref="TrainingOptionsGrid.SentenceClassifierTrainingOptionsGrid"/>.
		/// </remarks>
		public void OptimalTrain(
			TrainingOptionsGrid trainingOptionsGrid, 
			int foldCount, 
			double wordDropout,
			int wordDecimation,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism = 0)
		{
			if (trainingOptionsGrid == null) throw new ArgumentNullException("trainingOptionsGrid");

			if (trainingOptionsGrid.SentenceClassifierTrainingOptionsGrid.Count() != 1)
			{
				throw new ArgumentException("Currently only a single combination is supported for sentence training options.", "trainingOptionsGrid");
			}

			if (wordDropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (wordDecimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "wordDecimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var sentenceTrainingOptions = trainingOptionsGrid.SentenceClassifierTrainingOptionsGrid.First();

			var taggedWordFormTrainer = new Words.TaggedWordFormTrainer(this);

			this.WordClassifierBank = 
				taggedWordFormTrainer.OptimalTrain(trainingOptionsGrid.WordClassifierTrainingOptionsGrid, foldCount, wordDropout, wordDecimation, degreeOfParallelism);

			if (sentenceTrainingOptions.AnalogiesScoreOptions != null)
			{
				var wordFormsDictionaryBuilder = new Words.WordFormsDictionaryBuilder(this);

				this.WordFormsDictionary = wordFormsDictionaryBuilder.Build();
			}

			var taggedSentencesTrainer = new Sentences.TaggedSentenceTrainer(this);

			this.SentenceClassifier = taggedSentencesTrainer.Train(sentenceTrainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);
		}

		/// <summary>
		/// Train the <see cref="WordClassifierBank"/>of this inference resource optimally using cross-validation with the available training sources
		/// for this <see cref="InferenceResource.LanguageProvider"/>.
		/// </summary>
		/// <param name="trainingOptionsGrid">The grid of training options combinations for <see cref="WordClassifierBank"/>.</param>
		/// <param name="foldCount">The fold count used in K-fold cross-validation.</param>
		/// <param name="wordDropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="wordDecimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for tagged words training. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		public void OptimalTrainWordClassifierBank(
			IEnumerable<Words.WordClassifierTrainingOptions> trainingOptionsGrid, 
			int foldCount, 
			double wordDropout,
			int wordDecimation,
			int degreeOfParallelism = 0)
		{
			if (trainingOptionsGrid == null) throw new ArgumentNullException("trainingOptionsGrid");
			if (wordDropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (wordDecimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "wordDecimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var taggedWordFormTrainer = new Words.TaggedWordFormTrainer(this);

			this.WordClassifierBank = taggedWordFormTrainer.OptimalTrain(trainingOptionsGrid, foldCount, wordDropout, wordDecimation, degreeOfParallelism);
		}

		#endregion

		#region Load / Save

		/// <summary>
		/// Get the formatter for serializing an <see cref="InferenceResource"/>.
		/// During deserialization, the <see cref="TagType"/>, <see cref="Inflection"/> and <see cref="InflectionType"/>
		/// copies will be replaced with references provided by the <see cref="GrammarModel"/>
		/// of the given <paramref name="languageProvider"/>.
		/// </summary>
		/// <param name="languageProvider">The language provider.</param>
		/// <returns>Returns a formatter for serialization and deserialization.</returns>
		public static IFormatter GetFormatter(LanguageProvider languageProvider)
		{
			if (languageProvider == null) throw new ArgumentNullException("languageProvider");

			var formatter = new Gramma.Serialization.FastBinaryFormatter();

			var surrogateSelector = new SurrogateSelector();

			var streamingContext = new StreamingContext(StreamingContextStates.Persistence, languageProvider);

			surrogateSelector.AddSurrogate(
				typeof(Tag),
				streamingContext,
				new TagSerializationSurrogate());

			surrogateSelector.AddSurrogate(
				typeof(GrammarModel),
				streamingContext,
				new GrammarModelSerializationSurrogate());

			surrogateSelector.AddSurrogate(
				typeof(InflectionType),
				streamingContext,
				new InflectionTypeSerializationSurrogate());

			surrogateSelector.AddSurrogate(
				typeof(TagType),
				streamingContext,
				new TagTypeSerializationSurrogate());

			surrogateSelector.AddSurrogate(
				typeof(Inflection),
				streamingContext,
				new InflectionSerializationSurrogate());

			formatter.SurrogateSelector = surrogateSelector;
			formatter.Context = streamingContext;

			return formatter;
		}

		/// <summary>
		/// Save all the inference resource and all its contents to a stream.
		/// </summary>
		public void Save(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = GetFormatter(this.LanguageProvider);

			formatter.Serialize(stream, this);
		}

		/// <summary>
		/// Save all the inference resource and all its contents to a filename.
		/// </summary>
		/// <param name="filename">
		/// The name of the saved file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void Save(String filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenWriteStream(filename))
			{
				Save(stream);
			}
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.WordClassifierBank"/> property from a stream.
		/// </summary>
		public void LoadWordClassifierBank(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = GetFormatter();

			this.WordClassifierBank = (Words.WordClassifierBank)formatter.Deserialize(stream);
			this.WordClassifierBank.InferenceResource = this;
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.WordClassifierBank"/> property from a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void LoadWordClassifierBank(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenReadStream(filename))
			{
				LoadWordClassifierBank(stream);
			}
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.WordClassifierBank"/> property to a stream.
		/// </summary>
		public void SaveWordClassifierBank(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = this.GetFormatter();

			formatter.Serialize(stream, this.WordClassifierBank);
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.WordClassifierBank"/> property to a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void SaveWordClassifierBank(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenWriteStream(filename))
			{
				SaveWordClassifierBank(stream);
			}
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.WordFormsDictionary"/> property from a stream.
		/// </summary>
		public void LoadWordFormsDictionary(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = GetFormatter();

			this.WordFormsDictionary = (Words.WordFormsDictionary)formatter.Deserialize(stream);
			this.WordFormsDictionary.InferenceResource = this;
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.WordFormsDictionary"/> property from a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void LoadWordFormsDictionary(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenReadStream(filename))
			{
				LoadWordFormsDictionary(stream);
			}
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.WordFormsDictionary"/> property to a stream.
		/// </summary>
		public void SaveWordFormsDictionary(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = GetFormatter();

			formatter.Serialize(stream, this.WordFormsDictionary);
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.WordFormsDictionary"/> property to a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void SaveWordFormsDictionary(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenWriteStream(filename))
			{
				SaveWordFormsDictionary(stream);
			}
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.SentenceClassifier"/> property from a stream.
		/// </summary>
		/// <remarks>
		/// The <see cref="InferenceResource.WordClassifierBank"/> and <see cref="InferenceResource.WordFormsDictionary"/> properties
		/// should have been loaded or trained first.
		/// </remarks>
		public void LoadSentenceClassifier(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			if (this.WordClassifierBank == null)
				throw new InferenceException("The WordClassifierBank property should have been previously trained or loaded.");

			var formatter = GetFormatter();

			this.SentenceClassifier = (Sentences.SentenceClassifier)formatter.Deserialize(stream);
			this.SentenceClassifier.InferenceResource = this;
		}

		/// <summary>
		/// Load the <see cref="InferenceResource.SentenceClassifier"/> property from a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void LoadSentenceClassifier(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenReadStream(filename))
			{
				LoadSentenceClassifier(stream);
			}
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.SentenceClassifier"/> property to a stream.
		/// </summary>
		public void SaveSentenceClassifier(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var formatter = GetFormatter();

			formatter.Serialize(stream, this.SentenceClassifier);
		}

		/// <summary>
		/// Save the <see cref="InferenceResource.SentenceClassifier"/> property to a file.
		/// </summary>
		/// <param name="filename">
		/// The name of the file,
		/// optionally qualified to use a configured <see cref="DataStreaming.IStreamer"/>.
		/// </param>
		public void SaveSentenceClassifier(string filename)
		{
			if (filename == null) throw new ArgumentNullException("filename");

			using (var stream = DataStreaming.Configuration.StreamingEnvironment.OpenWriteStream(filename))
			{
				SaveSentenceClassifier(stream);
			}
		}

		#endregion

		#endregion

		#region Private methods

		/// <summary>
		/// Get a formatter via <see cref="GetFormatter(LanguageProvider)"/> 
		/// with added <see cref="InferenceResourceSerializationSurrogate"/>.
		/// All <see cref="InferenceResource"/> instances are mapped to this instance.
		/// </summary>
		private IFormatter GetFormatter()
		{
			var formatter = GetFormatter(this.LanguageProvider);

			var surrogateSelector = new SurrogateSelector();

			surrogateSelector.AddSurrogate(typeof(InferenceResource), formatter.Context, new InferenceResourceSerializationSurrogate(this));

			formatter.SurrogateSelector.ChainSelector(surrogateSelector);

			return formatter;
		}
		
		[OnDeserialized]
		private void OnDeserialized(StreamingContext ctx)
		{
			if (this.WordClassifierBank != null) this.WordClassifierBank.InferenceResource = this;
			if (this.WordFormsDictionary != null) this.WordFormsDictionary.InferenceResource = this;
			if (this.SentenceClassifier != null) this.SentenceClassifier.InferenceResource = this;
		}

		#endregion

		#region IKeyedElement<LanguageProvider> Members

		LanguageProvider IKeyedElement<LanguageProvider>.Key
		{
			get { return this.languageProvider; }
		}

		#endregion
	}
}
