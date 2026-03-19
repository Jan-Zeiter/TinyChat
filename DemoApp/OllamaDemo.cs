using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace DemoApp;

/// <summary>
/// Demonstrates IChatClient integration using OllamaSharp with Microsoft.Extensions.AI,
/// including tool support for getting the current time and weather.
/// </summary>
public static class OllamaDemo
{
	public const string MODELNAME = "qwen3.5:0.8b";

	/// <summary>
	/// Creates a service provider backed by a real Ollama IChatClient with function invocation enabled.
	/// The model is pulled automatically if it is not yet available locally.
	/// </summary>
	public static async Task<IServiceProvider> CreateServiceProviderWithOllamaChatClientAsync(
		IProgress<string>? progress = null,
		CancellationToken cancellationToken = default)
		=> await CreateServiceProviderWithOllamaChatClientAsync(MODELNAME, progress, cancellationToken);

	/// <summary>
	/// Creates a service provider backed by a real Ollama IChatClient with function invocation enabled.
	/// The model is pulled automatically if it is not yet available locally.
	/// </summary>
	/// <param name="modelName">The Ollama model name to use.</param>
	/// <param name="progress">Optional progress reporter for status messages.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	public static async Task<IServiceProvider> CreateServiceProviderWithOllamaChatClientAsync(
		string modelName,
		IProgress<string>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434"), modelName);

		await EnsureModelAvailableAsync(ollamaClient, modelName, progress, cancellationToken);

		var services = new ServiceCollection();

		services.AddChatClient((IChatClient)ollamaClient)
			.UseFunctionInvocation();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Returns ChatOptions pre-configured with the available tools.
	/// </summary>
	public static ChatOptions CreateChatOptions() => new()
	{
		Tools =
		[
			AIFunctionFactory.Create(GetCurrentTime),
			AIFunctionFactory.Create(GetCurrentWeather)
		]
	};

	private static async Task EnsureModelAvailableAsync(
		OllamaApiClient client,
		string modelName,
		IProgress<string>? progress,
		CancellationToken cancellationToken)
	{
		var models = await client.ListLocalModelsAsync(cancellationToken);
		var isAvailable = models.Any(m => m.Name.StartsWith(modelName, StringComparison.OrdinalIgnoreCase));

		if (!isAvailable)
		{
			progress?.Report($"Model '{modelName}' not found locally. Downloading...");

			await foreach (var status in client.PullModelAsync(modelName, cancellationToken))
			{
				if (!string.IsNullOrWhiteSpace(status?.Status))
					progress?.Report(status.Status);
			}

			progress?.Report($"Model '{modelName}' is ready.");
		}
		else
		{
			progress?.Report($"Model '{modelName}' is available.");
		}
	}

	/// <summary>
	/// Returns the names of all locally available Ollama models, sorted alphabetically.
	/// </summary>
	public static async Task<string[]> ListLocalModelNamesAsync(CancellationToken cancellationToken = default)
	{
		var ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434"));
		var models = await ollamaClient.ListLocalModelsAsync(cancellationToken);
		return models
			.Select(m => m.Name)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	[Description("Gets the current local date and time.")]
	private static string GetCurrentTime() =>
		DateTime.Now.ToString("ddd, MMM d yyyy HH:mm:ss");

	[Description("Gets the current weather for a given city with randomised data.")]
	private static async Task<string> GetCurrentWeather(
		[Description("The city name to get the weather for")] string city)
	{
		await Task.Delay(2000);

		var rng = new Random();
		var conditions = new[] { "sunny", "partly cloudy", "overcast", "light rain", "heavy rain", "snow", "foggy", "windy" };
		var temp = rng.Next(-10, 38);
		var humidity = rng.Next(30, 95);
		var condition = conditions[rng.Next(conditions.Length)];
		return $"Weather in {city}: {temp}\u00b0C, {condition}, humidity {humidity}%.";
	}
}
