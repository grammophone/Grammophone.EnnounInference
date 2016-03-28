# Gramma.Inference
This .NET library is a part-of-speech tagging and lemmatization system. This is a two-layer approach: The first layer proposes possible features for each word form in a sentence, the second layer takes into account the context of the word forms in the sentence and elects the most probable, which then become the keys for estimating the lemmata corresponding to the forms.

The library is language-agnostic, as it depends on providers honoring the contract enforced by [Gramma.LanguageModel](https://github.com/grammophone/Gramma.LanguageModel) to define the available languages and corresponding training sets.
Nevertheless, it is designed to deal with the computational challenges emerging from languages with rich morphology, and it is currently used in ancient Greek texts.

The central object type of the library is the `InferenceResource`. It is associated with a `LanguageProvider` defined via the [Gramma.LanguageModel](https://github.com/grammophone/Gramma.LanguageModel) system and provides methods to perform inference on a sentence using the `SentenceClassifier` which it offers via its homonymous property. The available `InferenceResource` instances are registered in the static property `InferenceEnvironment.InferenceResources`. They can be trained and saved using corresponding methods. Training references the data sets defined in the `InferenceEnvironment.Setup.TrainingSets` and `InferenceEnvironment.Setup.ValidationSets` static properties. Previously trained and saved instances can be loaded using the `Load` method of `InferenceResourceProvider` instances registered in the static property `InferenceEnvironment.Setup.InferenceResourceProviders`.

The previous are summarized in the following UML diagram.

![Inference setup](http://s7.postimg.org/s416gjyvv/Inference_setup.png)

This project relies on the following projects being in sibling directories:
* [Gramma.Caching](https://github.com/grammophone/Gramma.Caching)
* [Gramma.Configuration](https://github.com/grammophone/Gramma.Configuration)
* [Gramma.DataStreaming](https://github.com/grammophone/Gramma.DataStreaming)
* [Gramma.Vectors](https://github.com/grammophone/Gramma.Vectors)
* [Gramma.Optimization](https://github.com/grammophone/Gramma.Optimization)
* [Gramma.CRF](https://github.com/grammophone/Gramma.CRF)
* [Gramma.GenericContentModel](https://github.com/grammophone/Gramma.GenericContentModel)
* [Gramma.Indexing](https://github.com/grammophone/Gramma.Indexing)
* [Gramma.Kernels](https://github.com/grammophone/Gramma.Kernels)
* [Gramma.LanguageModel](https://github.com/grammophone/Gramma.LanguageModel)
* [Gramma.Linq](https://github.com/grammophone/Gramma.Linq)
* [Gramma.Parallel](https://github.com/grammophone/Gramma.Parallel)
* [Gramma.SVM](https://github.com/grammophone/Gramma.SVM)

