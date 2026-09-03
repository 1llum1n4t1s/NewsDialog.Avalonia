using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NewsDialog.Tests;

public sealed class NewsReliabilityTests
{
    [Fact]
    public async Task HttpSource_SkipsMalformedItemAndKeepsValidEmergency()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string json = """
            {
              "items": [
                {
                  "id": "emergency",
                  "title": "Emergency",
                  "publishedAt": "2026-01-01T00:00:00Z",
                  "severity": "Emergency",
                  "isBlocking": true
                },
                {
                  "id": "malformed",
                  "title": "Malformed",
                  "publishedAt": "not-a-date"
                }
              ]
            }
            """;

        using var http = new HttpClient(new StaticJsonHandler(json));
        var source = new HttpJsonNewsSource("https://example.test/news.json", http);

        var items = await source.FetchAsync(new NewsContext(), cancellationToken);

        var item = Assert.Single(items);
        Assert.Equal("emergency", item.Id);
        Assert.True(item.IsBlocking);
    }

    [Fact]
    public async Task RetryAfterFailure_ClearsFinalErrorAndResetsOutcome()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = new SequenceNewsSource(
            _ => Task.FromException<IReadOnlyList<NewsItem>>(new InvalidOperationException("first failure")),
            _ => Task.FromResult<IReadOnlyList<NewsItem>>(new[] { CreateItem("recovered") }));
        var viewModel = new NewsViewModel(source);

        await viewModel.LoadAsync(cancellationToken);

        Assert.Equal(NewsLoadState.Failed, viewModel.State);
        Assert.Equal(NewsOutcome.Failed, viewModel.FinalOutcome);
        Assert.NotNull(viewModel.FinalError);

        await viewModel.LoadAsync(cancellationToken);

        Assert.Equal(NewsLoadState.Loaded, viewModel.State);
        Assert.Equal(NewsOutcome.Closed, viewModel.FinalOutcome);
        Assert.Null(viewModel.FinalError);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task UnsupportedShellActionUrl_RaisesErrorWithoutReportingAction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var item = CreateItem("unsupported-action");
        item.ActionUrl = new Uri("file:///C:/not-a-browser-url");
        var options = new NewsOptions();
        Exception? reportedError = null;
        var actionCount = 0;
        options.ErrorOccurred += ex => reportedError = ex;
        options.ActionInvoked += _ => actionCount++;
        var viewModel = new NewsViewModel(new InMemoryNewsSource(item), options);
        await viewModel.LoadAsync(cancellationToken);

        viewModel.InvokeActionCommand.Execute(item);

        Assert.IsType<ArgumentException>(reportedError);
        Assert.Equal(0, actionCount);
        Assert.Equal(NewsOutcome.Closed, viewModel.FinalOutcome);
        Assert.Null(viewModel.ActionItem);
    }

    [Fact]
    public async Task VersionFiltering_NormalizesMissingComponentsAndFailsClosedWhenAppVersionIsUnknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var restricted = CreateItem("restricted");
        restricted.MaxAppVersion = "1.0.173";
        var unrestricted = CreateItem("unrestricted");
        var source = new InMemoryNewsSource(restricted, unrestricted);

        var normalized = await source.FetchAsync(new NewsContext { AppVersion = "1.0.173.0" }, cancellationToken);
        var malformed = await source.FetchAsync(new NewsContext { AppVersion = "1.0.173-beta" }, cancellationToken);
        var missing = await source.FetchAsync(new NewsContext(), cancellationToken);

        Assert.Contains(normalized, item => item.Id == "restricted");
        Assert.DoesNotContain(malformed, item => item.Id == "restricted");
        Assert.Contains(malformed, item => item.Id == "unrestricted");
        Assert.DoesNotContain(missing, item => item.Id == "restricted");
        Assert.Contains(missing, item => item.Id == "unrestricted");
    }

    [Fact]
    public async Task LocaleFiltering_FallsBackToParentTagWithoutWideningRegionalTarget()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var generic = CreateItem("generic");
        generic.Locales = new[] { "ja" };
        var regional = CreateItem("regional");
        regional.Locales = new[] { "ja-JP" };
        var source = new InMemoryNewsSource(generic, regional);

        var regionalRequest = await source.FetchAsync(new NewsContext { Locale = "JA-jp" }, cancellationToken);
        var genericRequest = await source.FetchAsync(new NewsContext { Locale = "ja" }, cancellationToken);

        Assert.Contains(regionalRequest, item => item.Id == "generic");
        Assert.Contains(regionalRequest, item => item.Id == "regional");
        Assert.Contains(genericRequest, item => item.Id == "generic");
        Assert.DoesNotContain(genericRequest, item => item.Id == "regional");
    }

    [Fact]
    public async Task UnsupportedContentUrl_FallsBackToInlineHtml()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var item = CreateItem("unsupported-content");
        item.ContentUrl = new Uri("file:///C:/not-a-browser-url");
        item.InlineHtml = "<p>Fallback content</p>";
        var viewModel = new NewsViewModel(new InMemoryNewsSource(item));

        await viewModel.LoadAsync(cancellationToken);

        var contentUri = viewModel.CurrentContentUri;
        Assert.NotNull(contentUri);
        Assert.Equal("data", contentUri!.Scheme);
    }

    [Fact]
    public async Task Cancellation_SettlesLoadingStateBeforeRethrowing()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var viewModel = new NewsViewModel(new CanceledNewsSource());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => viewModel.LoadAsync(cancellation.Token));

        Assert.Equal(NewsLoadState.Empty, viewModel.State);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task BlockingItems_AreAcknowledgedOneAtATimeAndDisableUserClose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = CreateBlockingItem("first", DateTimeOffset.UtcNow);
        var second = CreateBlockingItem("second", DateTimeOffset.UtcNow.AddMinutes(-1));
        var viewModel = new NewsViewModel(new InMemoryNewsSource(first, second));
        await viewModel.LoadAsync(cancellationToken);

        Assert.Same(first, viewModel.BlockingItem);
        Assert.False(viewModel.CanClose);

        viewModel.AcknowledgeBlockingCommand.Execute(null);

        Assert.Same(second, viewModel.BlockingItem);
        Assert.True(viewModel.ShowBlocking);
        Assert.False(viewModel.CanClose);

        viewModel.AcknowledgeBlockingCommand.Execute(null);

        Assert.Null(viewModel.BlockingItem);
        Assert.False(viewModel.ShowBlocking);
        Assert.True(viewModel.CanClose);
        Assert.Equal(NewsOutcome.Acknowledged, viewModel.FinalOutcome);
    }

    private static NewsItem CreateItem(string id)
        => new()
        {
            Id = id,
            Title = id,
            PublishedAt = DateTimeOffset.UtcNow,
        };

    private static NewsItem CreateBlockingItem(string id, DateTimeOffset publishedAt)
        => new()
        {
            Id = id,
            Title = id,
            PublishedAt = publishedAt,
            Severity = NewsSeverity.Emergency,
            IsBlocking = true,
        };

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private sealed class SequenceNewsSource(params Func<CancellationToken, Task<IReadOnlyList<NewsItem>>>[] responses) : INewsSource
    {
        private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<NewsItem>>>> _responses = new(responses);

        public Task<IReadOnlyList<NewsItem>> FetchAsync(NewsContext context, CancellationToken cancellationToken = default)
            => _responses.Dequeue()(cancellationToken);
    }

    private sealed class CanceledNewsSource : INewsSource
    {
        public Task<IReadOnlyList<NewsItem>> FetchAsync(NewsContext context, CancellationToken cancellationToken = default)
            => Task.FromCanceled<IReadOnlyList<NewsItem>>(cancellationToken);
    }
}
