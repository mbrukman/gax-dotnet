﻿/*
 * Copyright 2016 Google Inc. All Rights Reserved.
 * Use of this source code is governed by a BSD-style
 * license that can be found in the LICENSE file or at
 * https://developers.google.com/open-source/licenses/bsd
 */

using Google.Api.Gax.Testing;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Google.Api.Gax.Grpc.Tests
{
    public class PollingTest
    {
        [Fact]
        public void PollToCompletion_Success()
        {
            var pollSource = new PollSource(TimeSpan.FromSeconds(3), 5);
            var pollSettings = new PollSettings(Expiration.FromTimeout(TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(2));
            pollSource.Scheduler.Run(() =>
            {
                var result = pollSource.PollRepeatedly(pollSettings);
                Assert.Equal(5, result);
                Assert.Equal(TimeSpan.FromSeconds(4), pollSource.RunningTime);
            });
        }

        [Fact]
        public void PollToCompletion_Timeout()
        {
            var pollSource = new PollSource(TimeSpan.FromSeconds(10), 5);
            var pollSettings = new PollSettings(Expiration.FromTimeout(TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(2));
            pollSource.Scheduler.Run(() =>
            {
                Assert.Throws<TimeoutException>(() => pollSource.PollRepeatedly(pollSettings));
                // We give up at t=4 because the next call would be after the expiration.
                Assert.Equal(TimeSpan.FromSeconds(4), pollSource.RunningTime);
            });
        }

        [Fact]
        public async Task PollToCompletionAsync_Success()
        {
            var pollSource = new PollSource(TimeSpan.FromSeconds(3), 5);
            var pollSettings = new PollSettings(Expiration.FromTimeout(TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(2));
            await pollSource.Scheduler.RunAsync(async () =>
            {
                var result = await pollSource.PollRepeatedlyAsync(pollSettings);
                Assert.Equal(5, result);
                Assert.Equal(TimeSpan.FromSeconds(4), pollSource.RunningTime);
            });
        }

        [Fact]
        public async Task PollToCompletionAsync_Timeout()
        {
            var pollSource = new PollSource(TimeSpan.FromSeconds(10), 5);
            var pollSettings = new PollSettings(Expiration.FromTimeout(TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(2));
            await pollSource.Scheduler.RunAsync(async () =>
            {
                await Assert.ThrowsAsync<TimeoutException>(() => pollSource.PollRepeatedlyAsync(pollSettings));
                // We give up at t=4 because the next call would be after the expiration.
                Assert.Equal(TimeSpan.FromSeconds(4), pollSource.RunningTime);
            });
        }

        private class PollSource
        {
            public FakeScheduler Scheduler { get; } = new FakeScheduler();
            public FakeClock Clock => Scheduler.Clock;
            public DateTime CompletionTime { get; }
            public int FinalResult { get; }
            private readonly DateTime _startTime;
            public TimeSpan RunningTime => Clock.GetCurrentDateTimeUtc() - _startTime;

            public PollSource(TimeSpan timeToCompletion, int finalResult)
            {
                _startTime = Clock.GetCurrentDateTimeUtc();
                CompletionTime = _startTime + timeToCompletion;
                FinalResult = finalResult;
            }

            private int Poll(CallSettings callSettings) => Clock.GetCurrentDateTimeUtc() < CompletionTime ? 0 : FinalResult;

            private Task<int> PollAsync(CallSettings callSettings) => Task.FromResult(Poll(callSettings));

            // Simple predicate for whether an integer is positive.
            private static bool IsPositive(int input) => input > 0;

            internal int PollRepeatedly(PollSettings pollSettings)
                => Polling.PollRepeatedly(Poll, IsPositive, Clock, Scheduler, pollSettings);

            internal Task<int> PollRepeatedlyAsync(PollSettings pollSettings)
                => Polling.PollRepeatedlyAsync(PollAsync, IsPositive, Clock, Scheduler, pollSettings);
        }
    }
}