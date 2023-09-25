using SurrealDb.Exceptions;
using SurrealDb.Internals.Auth;
using SurrealDb.Internals.Constants;
using SurrealDb.Internals.Helpers;
using SurrealDb.Internals.Http;
using SurrealDb.Internals.Json;
using SurrealDb.Internals.Models;
using SurrealDb.Models;
using SurrealDb.Models.Auth;
using SurrealDb.Models.Response;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace SurrealDb.Internals;

internal class SurrealDbHttpEngine : ISurrealDbEngine
{
    private readonly Uri _uri;
    private readonly IHttpClientFactory? _httpClientFactory;
	private readonly Lazy<HttpClient> _singleHttpClient = new(() => new HttpClient(), true);
	private HttpClientConfiguration? _singleHttpClientConfiguration;
	private readonly SurrealDbHttpEngineConfig _config = new();

	public SurrealDbHttpEngine(Uri uri, IHttpClientFactory? httpClientFactory)
    {
        _uri = uri;
        _httpClientFactory = httpClientFactory;
    }

    public async Task Authenticate(Jwt jwt, CancellationToken cancellationToken)
    {
		using var wrapper = CreateHttpClientWrapper(new BearerAuth(jwt.Token));
		using var body = CreateBodyContent("RETURN TRUE");

		using var response = await wrapper.Instance.PostAsync("/sql", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);
		EnsuresFirstResultOk(dbResponse);

		_config.SetBearerAuth(jwt.Token);
	}

	public void Configure(string? ns, string? db, string? username, string? password)
	{
		if (ns is not null)
			_config.Use(ns, db);

		if (username is not null)
			_config.SetBasicAuth(username, password);
	}
	public void Configure(string? ns, string? db, string? token = null)
	{
		if (ns is not null)
			_config.Use(ns, db);

		if (token is not null)
			_config.SetBearerAuth(token);
	}

	public async Task Connect(CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent("RETURN TRUE");

		using var response = await wrapper.Instance.PostAsync("/sql", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);
		EnsuresFirstResultOk(dbResponse);
	}

    public async Task<T> Create<T>(T data, CancellationToken cancellationToken) where T : Record
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(data);

		if (data.Id is null)
			throw new SurrealDbException("Cannot create a record without an Id");

		using var response = await wrapper.Instance.PostAsync($"/key/{data.Id.Table}/{data.Id.UnescapedId}", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<T>>(dbResponse)!;
		return list.First();
	}
    public async Task<T> Create<T>(string table, T? data, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = data is null ? new StringContent("{}") : CreateBodyContent(data);

		using var response = await wrapper.Instance.PostAsync($"/key/{table}", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<T>>(dbResponse)!;
		return list.First();
	}

	public async Task Delete(string table, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();

		using var response = await wrapper.Instance.DeleteAsync($"/key/{table}", cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);
		EnsuresFirstResultOk(dbResponse);
	}
	public async Task<bool> Delete(Thing thing, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();

		using var response = await wrapper.Instance.DeleteAsync($"/key/{thing.Table}/{thing.UnescapedId}", cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<object>>(dbResponse)!;
		return list.Any(r => r is not null);
    }

	public void Dispose()
	{
		if (_singleHttpClient.IsValueCreated)
			_singleHttpClient.Value.Dispose();
	}

	public async Task<bool> Health(CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();

		try
		{
			using var response = await wrapper.Instance.GetAsync("/health", cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
	}

	public Task Invalidate(CancellationToken _)
	{
        _config.ResetAuth();
		return Task.CompletedTask;
    }

	public async Task<TOutput> Merge<TMerge, TOutput>(TMerge data, CancellationToken cancellationToken) where TMerge : Record
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(data);

		if (data.Id is null)
			throw new SurrealDbException("Cannot create a record without an Id");

		using var response = await wrapper.Instance.PatchAsync($"/key/{data.Id.Table}/{data.Id.UnescapedId}", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<TOutput>>(dbResponse)!;
		return list.First();
	}
	public async Task<T> Merge<T>(Thing thing, Dictionary<string, object> data, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(data);

		using var response = await wrapper.Instance.PatchAsync($"/key/{thing.Table}/{thing.UnescapedId}", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<T>>(dbResponse)!;
		return list.First();
	}

	public async Task<SurrealDbResponse> Query(
		string query,
		IReadOnlyDictionary<string, object> parameters,
		CancellationToken cancellationToken
	)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(query);

		var queryString = HttpUtility.ParseQueryString(string.Empty);

		foreach (var (key, value) in _config.Parameters)
		{
			queryString[key] = JsonSerializer.Serialize(value, SurrealDbSerializerOptions.Default);
		}
		foreach (var (key, value) in parameters)
		{
			queryString[key] = JsonSerializer.Serialize(value, SurrealDbSerializerOptions.Default);
		}

		var uriBuilder = new UriBuilder
		{
			Scheme = string.Empty,
			Host = string.Empty,
			Path = "/sql",
			Query = queryString.ToString()
		};
		var requestUri = uriBuilder.ToString();

		using var response = await wrapper.Instance.PostAsync(requestUri, body, cancellationToken);

		return await DeserializeDbResponseAsync(response, cancellationToken);
	}

	public async Task<List<T>> Select<T>(string table, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();

		using var response = await wrapper.Instance.GetAsync($"/key/{table}", cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);
		return ExtractFirstResultValue<List<T>>(dbResponse)!;
	}
    public async Task<T?> Select<T>(Thing thing, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();

		using var response = await wrapper.Instance.GetAsync($"/key/{thing.Table}/{thing.UnescapedId}", cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<T>>(dbResponse)!;
		return list.FirstOrDefault();
    }

    public async Task Set(string key, object value, CancellationToken cancellationToken)
    {
        var dbResponse = await Query(
            $"RETURN ${key}", 
            new Dictionary<string, object>() { { key, value } }, 
            cancellationToken
        );
		EnsuresFirstResultOk(dbResponse);

		_config.SetParam(key, value);
    }

    public async Task SignIn(RootAuth rootAuth, CancellationToken cancellationToken)
    {
        using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(rootAuth);

		using var response = await wrapper.Instance.PostAsync("/signin", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        _config.SetBasicAuth(rootAuth.Username, rootAuth.Password);
    }
	public async Task<Jwt> SignIn(NamespaceAuth nsAuth, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(nsAuth);

		using var response = await wrapper.Instance.PostAsync("/signin", body, cancellationToken);
		response.EnsureSuccessStatusCode();

		var result = await DeserializeAuthResponse(response, cancellationToken);

		_config.SetBearerAuth(result.Token!);

		return new Jwt { Token = result.Token! };
	}
	public async Task<Jwt> SignIn(DatabaseAuth dbAuth, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(dbAuth);

		using var response = await wrapper.Instance.PostAsync("/signin", body, cancellationToken);
		response.EnsureSuccessStatusCode();

		var result = await DeserializeAuthResponse(response, cancellationToken);

		_config.SetBearerAuth(result.Token!);

		return new Jwt { Token = result.Token! };
	}
	public async Task<Jwt> SignIn<T>(T scopeAuth, CancellationToken cancellationToken) where T : ScopeAuth
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(scopeAuth);

		using var response = await wrapper.Instance.PostAsync("/signin", body, cancellationToken);
		response.EnsureSuccessStatusCode();

		var result = await DeserializeAuthResponse(response, cancellationToken);

		_config.SetBearerAuth(result.Token!);

		return new Jwt { Token = result.Token! };
	}

	public async Task<Jwt> SignUp<T>(T scopeAuth, CancellationToken cancellationToken) where T : ScopeAuth
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(scopeAuth);

		using var response = await wrapper.Instance.PostAsync("/signup", body, cancellationToken);
		response.EnsureSuccessStatusCode();

		var result = await DeserializeAuthResponse(response, cancellationToken);

		return new Jwt { Token = result.Token! };
	}

	public Task Unset(string key, CancellationToken _)
	{
        _config.RemoveParam(key);
		return Task.CompletedTask;
    }

	public async Task<T> Upsert<T>(T data, CancellationToken cancellationToken) where T : Record
	{
		using var wrapper = CreateHttpClientWrapper();
		using var body = CreateBodyContent(data);

		if (data.Id is null)
			throw new SurrealDbException("Cannot create a record without an Id");

		using var response = await wrapper.Instance.PutAsync($"/key/{data.Id.Table}/{data.Id.UnescapedId}", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);

		var list = ExtractFirstResultValue<List<T>>(dbResponse)!;
		return list.First();
	}

	public async Task Use(string ns, string db, CancellationToken cancellationToken)
	{
		using var wrapper = CreateHttpClientWrapper(null, new UseConfiguration { Ns = ns, Db = db });
		using var body = CreateBodyContent("RETURN TRUE");

		using var response = await wrapper.Instance.PostAsync("/sql", body, cancellationToken);

		var dbResponse = await DeserializeDbResponseAsync(response, cancellationToken);
		EnsuresFirstResultOk(dbResponse);

		_config.Use(ns, db);
    }

	public async Task<string> Version(CancellationToken _)
	{
		using var wrapper = CreateHttpClientWrapper();
		return await wrapper.Instance.GetStringAsync("/version");
	}

	private HttpClientWrapper CreateHttpClientWrapper(IAuth? overridedAuth = null, UseConfiguration? useConfiguration = null)
	{
		var client = CreateHttpClient(overridedAuth, useConfiguration);
		bool shouldDispose = !IsSingleHttpClient(client);

		return new HttpClientWrapper(client, shouldDispose);
	}

	private HttpClient CreateHttpClient(IAuth? overridedAuth, UseConfiguration? useConfiguration)
	{
		string? ns = useConfiguration is not null ? useConfiguration.Ns : _config.Ns;
		string? db = useConfiguration is not null ? useConfiguration.Db : _config.Db;

		var client = GetHttpClient();

		bool isSingleHttpClient = IsSingleHttpClient(client);

		if (isSingleHttpClient)
		{
			if (TrySetSingleHttpClientConfiguration(ns, db, _config.Auth))
			{
				ApplyHttpClientConfiguration(client, overridedAuth, useConfiguration);
				return client;
			}

			var desiredClientConfiguration = new HttpClientConfiguration(ns, db, overridedAuth ?? _config.Auth);
			bool shouldClone = _singleHttpClientConfiguration != desiredClientConfiguration;

			if (shouldClone)
			{
				var newHttpClient = new HttpClient();
				ApplyHttpClientConfiguration(newHttpClient, overridedAuth, useConfiguration);

				return newHttpClient;
			}
		}
		else
		{
			ApplyHttpClientConfiguration(client, overridedAuth, useConfiguration);
		}

		return client;
	}

	private void ApplyHttpClientConfiguration(HttpClient client, IAuth? overridedAuth, UseConfiguration? useConfiguration)
	{
		client.BaseAddress = _uri;

		client.DefaultRequestHeaders.Remove(HttpConstants.ACCEPT_HEADER_NAME);
		client.DefaultRequestHeaders.Remove(HttpConstants.NS_HEADER_NAME);
		client.DefaultRequestHeaders.Remove(HttpConstants.DB_HEADER_NAME);

		client.DefaultRequestHeaders.Add(HttpConstants.ACCEPT_HEADER_NAME, HttpConstants.ACCEPT_HEADER_VALUES);

		var ns = useConfiguration is not null ? useConfiguration.Ns : _config.Ns;
		var db = useConfiguration is not null ? useConfiguration.Db : _config.Db;

		client.DefaultRequestHeaders.Add(HttpConstants.NS_HEADER_NAME, ns);
		client.DefaultRequestHeaders.Add(HttpConstants.DB_HEADER_NAME, db);

		var auth = overridedAuth ?? _config.Auth;

		if (auth is BearerAuth bearerAuth)
		{
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthConstants.BEARER, bearerAuth.Token);
		}
		if (auth is BasicAuth basicAuth)
		{
			string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{basicAuth.Username}:{basicAuth.Password}"));
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthConstants.BASIC, credentials);
		}
		if (auth is NoAuth)
		{
			client.DefaultRequestHeaders.Authorization = null;
		}
	}

	private HttpClient GetHttpClient()
    {
		if (_httpClientFactory is not null)
		{
			string httpClientName = HttpClientHelper.GetHttpClientName(_uri);
			return _httpClientFactory.CreateClient(httpClientName);
		}

		return _singleHttpClient.Value;
	}

	private readonly object _singleHttpClientConfigurationLock = new();
	private bool TrySetSingleHttpClientConfiguration(string? ns, string? db, IAuth auth)
	{
		lock (_singleHttpClientConfigurationLock)
		{
			if (_singleHttpClientConfiguration is null)
			{
				_singleHttpClientConfiguration = new HttpClientConfiguration(ns, db, auth);
				return true;
			}

			return false;
		}
	}

	private bool IsSingleHttpClient(HttpClient client)
	{
		return _singleHttpClient.IsValueCreated && client == _singleHttpClient.Value;
	}

	private static StringContent CreateBodyContent<T>(T data)
	{
		string bodyContent = data is string str
			? str
			: JsonSerializer.Serialize(data, SurrealDbSerializerOptions.Default);

		return new StringContent(bodyContent, Encoding.UTF8, "application/json");
	}

	private static async Task<SurrealDbResponse> DeserializeDbResponseAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken
	)
	{
#if NET6_0_OR_GREATER
		using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
        using var stream = await response.Content.ReadAsStreamAsync();
#endif

		if (!response.IsSuccessStatusCode)
		{
			var result = await JsonSerializer.DeserializeAsync<ISurrealDbResult>(stream, SurrealDbSerializerOptions.Default, cancellationToken);
			return new SurrealDbResponse(result!);
		}

		var list = new List<ISurrealDbResult>();

		await foreach (var result in JsonSerializer.DeserializeAsyncEnumerable<ISurrealDbResult>(stream, SurrealDbSerializerOptions.Default, cancellationToken))
		{
			if (result is not null)
				list.Add(result);
		}

		return new SurrealDbResponse(list);
	}

	private async Task<AuthResponse> DeserializeAuthResponse(
		HttpResponseMessage response,
		CancellationToken cancellationToken
	)
	{
#if NET6_0_OR_GREATER
		using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
		using var stream = await response.Content.ReadAsStreamAsync();
#endif

		var authResponse = await JsonSerializer.DeserializeAsync<AuthResponse>(
			stream,
			SurrealDbSerializerOptions.Default,
			cancellationToken
		);

		if (authResponse is null)
			throw new SurrealDbException("Cannot deserialize auth response");

		return authResponse;
	}

	private static SurrealDbOkResult EnsuresFirstResultOk(SurrealDbResponse dbResponse)
	{
		if (dbResponse.IsEmpty)
			throw new EmptySurrealDbResponseException();

		var firstResult = dbResponse.FirstResult ?? throw new SurrealDbErrorResultException();

		if (firstResult is ISurrealDbErrorResult errorResult)
			throw new SurrealDbErrorResultException(errorResult);

		return (SurrealDbOkResult)firstResult;
	}
	private static T? ExtractFirstResultValue<T>(SurrealDbResponse dbResponse)
	{
		var okResult = EnsuresFirstResultOk(dbResponse);
		return okResult.GetValue<T>();
	}
}