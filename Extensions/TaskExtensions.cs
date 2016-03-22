using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Gramma.Inference.Extensions
{
	/// <summary>
	/// Extensions for <see cref="Task"/>.
	/// </summary>
	public static class TaskExtensions
	{
		/// <summary>
		/// Serves dual purpose. First, write the exception to trace listeners. 
		/// Second, this marks the exception as handled and prevents the application from dying when the task
		/// exception is unhandled. 
		/// </summary>
		/// <typeparam name="T">The type of object returned by the task.</typeparam>
		/// <param name="task">The task whose exception is to be handled.</param>
		public static void AppendTraceExceptionHandler<T>(this Task<T> task)
		{
			task.ContinueWith(t =>
				{
					if (t.Exception != null)
					{
						t.Exception.ToTrace();
					}
				},
				TaskContinuationOptions.OnlyOnFaulted);
		}

		/// <summary>
		/// Write exception information to the Trace listeners.
		/// </summary>
		public static void ToTrace(this Exception exception)
		{
			if (exception == null) throw new ArgumentNullException("exception");

			var aggregateException = exception as AggregateException;

			if (aggregateException != null)
			{
				aggregateException.Flatten();

				foreach (var subexception in aggregateException.InnerExceptions)
				{
					ToTrace(exception);
				}
			}
			else
			{
				string traceLine;

				if (exception.Source != null && exception.Source.Length > 0)
				{
					traceLine = 
						String.Format("Exception type {0}, Message: {1}, Source: {2}.", exception.GetType().FullName, exception.Message, exception.Source);
				}
				else
				{
					traceLine =
						String.Format("Exception type {0}, Message: {1}.", exception.GetType().FullName, exception.Message);
				}

				Trace.WriteLine(traceLine);
			}
		}
	}
}
