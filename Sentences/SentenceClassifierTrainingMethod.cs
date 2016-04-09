using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// The training method to use for <see cref="SentenceClassifier"/>.
	/// </summary>
	public enum SentenceClassifierTrainingMethod
	{
		/// <summary>
		/// Use offline training, using the options
		/// specified in <see cref="SentenceClassifierTrainingOptions.OfflineTrainingOptions"/>.
		/// </summary>
		Offline,

		/// <summary>
		/// Use online training, using the options
		/// specified in <see cref="SentenceClassifierTrainingOptions.OnlineTrainingOptions"/>.
		/// </summary>
		Online
	}

}
