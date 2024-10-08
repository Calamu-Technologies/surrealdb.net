﻿using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using SurrealDb.Net.LiveQuery.Tests.Abstract;
using SurrealDb.Net.LiveQuery.Tests.Models;
using SurrealDb.Net.Models.LiveQuery;

namespace SurrealDb.Net.LiveQuery.Tests;

public class ReactiveObserveLiveQueryTests : BaseLiveQueryTests
{
    [Theory]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=JSON")]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=CBOR")]
    public async Task ShouldObserveQuery(string connectionString)
    {
        var allResults = new List<SurrealDbLiveQueryResponse>();

        Func<Task> func = async () =>
        {
            await using var surrealDbClientGenerator = new SurrealDbClientGenerator();
            var dbInfo = surrealDbClientGenerator.GenerateDatabaseInfo();

            using var client = surrealDbClientGenerator.Create(connectionString);
            await client.SignIn(new RootAuth { Username = "root", Password = "root" });
            await client.Use(dbInfo.Namespace, dbInfo.Database);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(Timeout);

            var testScheduler = new TestScheduler();
            var completionSource = new TaskCompletionSource<bool>();

            cts.Token.Register(() =>
            {
                completionSource.TrySetCanceled();
            });

            var coldObservable = client
                .ObserveQuery<TestRecord>($"LIVE SELECT * FROM test;")
                .Publish()
                .RefCount();

            using var _ = coldObservable.SubscribeOn(testScheduler).Subscribe(allResults.Add);

            await Task.Yield();

            using var __ = coldObservable
                .OfType<SurrealDbLiveQueryOpenResponse>()
                .Take(1)
                .Select(_ =>
                    Observable.FromAsync(async () =>
                    {
                        var record = await client.Create("test", new TestRecord { Value = 1 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Upsert(new TestRecord { Id = record.Id, Value = 2 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Delete(record.Id!);
                        await WaitLiveQueryNotificationAsync();
                    })
                )
                .Merge()
                .SubscribeOn(testScheduler)
                .Subscribe(_ =>
                {
                    completionSource.SetResult(true);
                });

            testScheduler.Start();

            await completionSource.Task;
        };

        await func.Should().NotThrowAsync();

        allResults.Should().HaveCount(4);

        var firstResult = allResults[0];
        firstResult.Should().BeOfType<SurrealDbLiveQueryOpenResponse>();

        var secondResult = allResults[1];
        secondResult.Should().BeOfType<SurrealDbLiveQueryCreateResponse<TestRecord>>();

        var thirdResult = allResults[2];
        thirdResult.Should().BeOfType<SurrealDbLiveQueryUpdateResponse<TestRecord>>();

        var lastResult = allResults[3];
        lastResult.Should().BeOfType<SurrealDbLiveQueryDeleteResponse<TestRecord>>();
    }

    [Theory]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=JSON")]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=CBOR")]
    public async Task ShouldObserveRawQuery(string connectionString)
    {
        var allResults = new List<SurrealDbLiveQueryResponse>();

        Func<Task> func = async () =>
        {
            await using var surrealDbClientGenerator = new SurrealDbClientGenerator();
            var dbInfo = surrealDbClientGenerator.GenerateDatabaseInfo();

            using var client = surrealDbClientGenerator.Create(connectionString);
            await client.SignIn(new RootAuth { Username = "root", Password = "root" });
            await client.Use(dbInfo.Namespace, dbInfo.Database);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(Timeout);

            var testScheduler = new TestScheduler();
            var completionSource = new TaskCompletionSource<bool>();

            cts.Token.Register(() =>
            {
                completionSource.TrySetCanceled();
            });

            var coldObservable = client
                .ObserveRawQuery<TestRecord>("LIVE SELECT * FROM test;")
                .Publish()
                .RefCount();

            using var _ = coldObservable
                .SubscribeOn(testScheduler)
                .Subscribe(
                    allResults.Add,
                    e =>
                    {
                        e.Should().BeNull();
                    }
                );

            await Task.Yield();

            using var __ = coldObservable
                .OfType<SurrealDbLiveQueryOpenResponse>()
                .Take(1)
                .Select(_ =>
                    Observable.FromAsync(async () =>
                    {
                        var record = await client.Create("test", new TestRecord { Value = 1 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Upsert(new TestRecord { Id = record.Id, Value = 2 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Delete(record.Id!);
                        await WaitLiveQueryNotificationAsync();
                    })
                )
                .Merge()
                .SubscribeOn(testScheduler)
                .Subscribe(
                    _ =>
                    {
                        completionSource.SetResult(true);
                    },
                    e =>
                    {
                        e.Should().BeNull();
                    }
                );

            testScheduler.Start();

            await completionSource.Task;
        };

        await func.Should().NotThrowAsync();

        allResults.Should().HaveCount(4);

        var firstResult = allResults[0];
        firstResult.Should().BeOfType<SurrealDbLiveQueryOpenResponse>();

        var secondResult = allResults[1];
        secondResult.Should().BeOfType<SurrealDbLiveQueryCreateResponse<TestRecord>>();

        var thirdResult = allResults[2];
        thirdResult.Should().BeOfType<SurrealDbLiveQueryUpdateResponse<TestRecord>>();

        var lastResult = allResults[3];
        lastResult.Should().BeOfType<SurrealDbLiveQueryDeleteResponse<TestRecord>>();
    }

    [Theory]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=JSON")]
    [InlineData("Endpoint=ws://127.0.0.1:8000/rpc;User=root;Pass=root;Serialization=CBOR")]
    public async Task ShouldObserveTable(string connectionString)
    {
        var allResults = new List<SurrealDbLiveQueryResponse>();

        Func<Task> func = async () =>
        {
            await using var surrealDbClientGenerator = new SurrealDbClientGenerator();
            var dbInfo = surrealDbClientGenerator.GenerateDatabaseInfo();

            using var client = surrealDbClientGenerator.Create(connectionString);
            await client.SignIn(new RootAuth { Username = "root", Password = "root" });
            await client.Use(dbInfo.Namespace, dbInfo.Database);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(Timeout);

            var testScheduler = new TestScheduler();
            var completionSource = new TaskCompletionSource<bool>();

            cts.Token.Register(() =>
            {
                completionSource.TrySetCanceled();
            });

            var coldObservable = client.ObserveTable<TestRecord>("test").Publish().RefCount();

            using var _ = coldObservable
                .SubscribeOn(testScheduler)
                .Subscribe(
                    allResults.Add,
                    e =>
                    {
                        e.Should().BeNull();
                    }
                );

            await Task.Yield();

            using var __ = coldObservable
                .OfType<SurrealDbLiveQueryOpenResponse>()
                .Take(1)
                .Select(_ =>
                    Observable.FromAsync(async () =>
                    {
                        var record = await client.Create("test", new TestRecord { Value = 1 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Upsert(new TestRecord { Id = record.Id, Value = 2 });
                        await WaitLiveQueryNotificationAsync();

                        await client.Delete(record.Id!);
                        await WaitLiveQueryNotificationAsync();
                    })
                )
                .Merge()
                .SubscribeOn(testScheduler)
                .Subscribe(
                    _ =>
                    {
                        completionSource.SetResult(true);
                    },
                    e =>
                    {
                        e.Should().BeNull();
                    }
                );

            testScheduler.Start();

            await completionSource.Task;
        };

        await func.Should().NotThrowAsync();

        allResults.Should().HaveCount(4);

        var firstResult = allResults[0];
        firstResult.Should().BeOfType<SurrealDbLiveQueryOpenResponse>();

        var secondResult = allResults[1];
        secondResult.Should().BeOfType<SurrealDbLiveQueryCreateResponse<TestRecord>>();

        var thirdResult = allResults[2];
        thirdResult.Should().BeOfType<SurrealDbLiveQueryUpdateResponse<TestRecord>>();

        var lastResult = allResults[3];
        lastResult.Should().BeOfType<SurrealDbLiveQueryDeleteResponse<TestRecord>>();
    }
}
