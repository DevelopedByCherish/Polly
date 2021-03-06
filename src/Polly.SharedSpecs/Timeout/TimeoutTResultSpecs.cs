﻿using FluentAssertions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Polly.Specs.Helpers;
using Polly.Timeout;
using Polly.Utilities;
using Xunit;

namespace Polly.Specs.Timeout
{
    public class TimeoutTResultSpecs
    {
        #region Configuration

        [Fact]
        public void Should_throw_when_timeout_is_zero_by_timespan()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(TimeSpan.Zero);

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("timeout");
        }

        [Fact]
        public void Should_throw_when_timeout_is_zero_by_seconds()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(0);

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("seconds");
        }

        [Fact]
        public void Should_throw_when_timeout_is_less_than_zero_by_timespan()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(-TimeSpan.FromHours(1));

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("timeout");
        }

        [Fact]
        public void Should_throw_when_timeout_is_less_than_zero_by_seconds()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(-10);

            policy.ShouldThrow<ArgumentOutOfRangeException>().And
                .ParamName.Should().Be("seconds");
        }

        [Fact]
        public void Should_not_throw_when_timeout_is_greater_than_zero_by_timespan()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(TimeSpan.FromMilliseconds(1));

            policy.ShouldNotThrow();
        }

        [Fact]
        public void Should_not_throw_when_timeout_is_greater_than_zero_by_seconds()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(3);

            policy.ShouldNotThrow();
        }

        [Fact]
        public void Should_not_throw_when_timeout_is_maxvalue()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(TimeSpan.MaxValue);

            policy.ShouldNotThrow();
        }

        [Fact]
        public void Should_not_throw_when_timeout_seconds_is_maxvalue()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(int.MaxValue);

            policy.ShouldNotThrow();
        }

        [Fact]
        public void Should_throw_when_onTimeout_is_null_with_timespan()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(TimeSpan.FromMinutes(0.5), null);

            policy.ShouldThrow<ArgumentNullException>()
                .And.ParamName.Should().Be("onTimeout");
        }

        [Fact]
        public void Should_throw_when_onTimeout_is_null_with_seconds()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(30, null);

            policy.ShouldThrow<ArgumentNullException>()
                .And.ParamName.Should().Be("onTimeout");
        }

        [Fact]
        public void Should_throw_when_timeoutProvider_is_null()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>((Func<TimeSpan>)null);

            policy.ShouldThrow<ArgumentNullException>()
                .And.ParamName.Should().Be("timeoutProvider");
        }

        [Fact]
        public void Should_throw_when_onTimeout_is_null_with_timeoutprovider()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(() => TimeSpan.FromSeconds(30), null);

            policy.ShouldThrow<ArgumentNullException>()
                .And.ParamName.Should().Be("onTimeout");
        }

        [Fact]
        public void Should_be_able_to_configure_with_timeout_func()
        {
            Action policy = () => Policy.Timeout<ResultPrimitive>(() => TimeSpan.FromSeconds(1));

            policy.ShouldNotThrow();
        }

        #endregion

        #region Timeout operation - pessimistic

        [Fact]
        public void Should_throw_when_timeout_is_less_than_execution_duration__pessimistic()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(50);

            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Pessimistic);

            policy.Invoking(p => p.Execute(() =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                return ResultPrimitive.WhateverButTooLate;
            })).ShouldThrow<TimeoutRejectedException>();
        }

        [Fact]
        public void Should_not_throw_when_timeout_is_greater_than_execution_duration__pessimistic()
        {
            var policy = Policy.Timeout<ResultPrimitive>(TimeSpan.FromSeconds(1), TimeoutStrategy.Pessimistic);

            ResultPrimitive result = ResultPrimitive.Undefined;
            policy.Invoking(p =>
            {
                result = p.Execute(() => ResultPrimitive.Good);
            }).ShouldNotThrow();

            result.Should().Be(ResultPrimitive.Good);
        }

        [Fact]
        public void Should_throw_timeout_after_correct_duration__pessimistic()
        {
            Stopwatch watch = new Stopwatch();

            TimeSpan timeout = TimeSpan.FromSeconds(1);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Pessimistic);

            TimeSpan tolerance = TimeSpan.FromSeconds(3); // Consider increasing tolerance, if test fails transiently in different test/build environments.

            watch.Start();
            policy.Invoking(p => p.Execute(() =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(10), CancellationToken.None);
                return ResultPrimitive.WhateverButTooLate;
            }))
                .ShouldThrow<TimeoutRejectedException>();
            watch.Stop();

            watch.Elapsed.Should().BeCloseTo(timeout, ((int)tolerance.TotalMilliseconds));
        }

        #endregion

        #region Timeout operation - optimistic

        [Fact]
        public void Should_throw_when_timeout_is_less_than_execution_duration__optimistic()
        {
            var policy = Policy.Timeout<ResultPrimitive>(TimeSpan.FromMilliseconds(50), TimeoutStrategy.Optimistic);
            var userCancellationToken = CancellationToken.None;

            policy.Invoking(p => p.Execute(ct =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(3), ct);
                return ResultPrimitive.WhateverButTooLate;
            }, userCancellationToken))
            .ShouldThrow<TimeoutRejectedException>();
        }

        [Fact]
        public void Should_not_throw_when_timeout_is_greater_than_execution_duration__optimistic()
        {
            var policy = Policy.Timeout<ResultPrimitive>(TimeSpan.FromSeconds(1), TimeoutStrategy.Optimistic);
            var userCancellationToken = CancellationToken.None;

            ResultPrimitive result = ResultPrimitive.Undefined;
            policy.Invoking(p =>
            {
                result = p.Execute(ct => ResultPrimitive.Good, userCancellationToken);
            }).ShouldNotThrow();

            result.Should().Be(ResultPrimitive.Good);
        }

        [Fact]
        public void Should_throw_timeout_after_correct_duration__optimistic()
        {
            Stopwatch watch = new Stopwatch();

            TimeSpan timeout = TimeSpan.FromSeconds(1);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Optimistic);
            var userCancellationToken = CancellationToken.None;

            TimeSpan tolerance = TimeSpan.FromSeconds(3); // Consider increasing tolerance, if test fails transiently in different test/build environments.

            watch.Start();
            policy.Invoking(p => p.Execute(ct =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(10), ct);
                return ResultPrimitive.WhateverButTooLate;
            }, userCancellationToken))
                .ShouldThrow<TimeoutRejectedException>();
            watch.Stop();

            watch.Elapsed.Should().BeCloseTo(timeout, ((int)tolerance.TotalMilliseconds));
        }

        #endregion

        #region Non-timeout cancellation - pessimistic (user-delegate does not observe cancellation)

        [Fact]
        public void Should_not_be_able_to_cancel_with_user_cancellation_token_before_timeout__pessimistic()
        {
            Stopwatch watch = new Stopwatch();

            int timeout = 5;
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Pessimistic);

            TimeSpan tolerance = TimeSpan.FromSeconds(3); // Consider increasing tolerance, if test fails transiently in different test/build environments.

            TimeSpan userTokenExpiry = TimeSpan.FromSeconds(1); // Use of time-based token irrelevant to timeout policy; we just need some user token that cancels independently of policy's internal token.
            using (CancellationTokenSource userTokenSource = new CancellationTokenSource(userTokenExpiry))
            {
                watch.Start();
                policy.Invoking(p => p.Execute(
                    _ => { SystemClock.Sleep(TimeSpan.FromSeconds(timeout * 2), CancellationToken.None); // Do not observe any cancellation in the middle of execution
                        return ResultPrimitive.WhateverButTooLate;
                    }, userTokenSource.Token) // ... with user token.
                   ).ShouldThrow<TimeoutRejectedException>();
                watch.Stop();
            }

            watch.Elapsed.Should().BeCloseTo(TimeSpan.FromSeconds(timeout), ((int)tolerance.TotalMilliseconds));

        }

        [Fact]
        public void Should_not_execute_user_delegate_if_user_cancellationtoken_cancelled_before_delegate_reached__pessimistic()
        {
            var policy = Policy.Timeout<ResultPrimitive>(10, TimeoutStrategy.Pessimistic);

            bool executed = false;

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();

                policy.Invoking(p => p.Execute(ct =>
                {
                    executed = true;
                    return ResultPrimitive.WhateverButTooLate;
                }, cts.Token))
                .ShouldThrow<OperationCanceledException>();
            }

            executed.Should().BeFalse();
        }

        #endregion

        #region Non-timeout cancellation - optimistic (user-delegate observes cancellation)

        [Fact]
        public void Should_be_able_to_cancel_with_user_cancellation_token_before_timeout__optimistic()
        {
            Stopwatch watch = new Stopwatch();

            int timeout = 10;
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Optimistic);

            TimeSpan tolerance = TimeSpan.FromSeconds(3); // Consider increasing tolerance, if test fails transiently in different test/build environments.

            TimeSpan userTokenExpiry = TimeSpan.FromSeconds(1); // Use of time-based token irrelevant to timeout policy; we just need some user token that cancels independently of policy's internal token.
            using (CancellationTokenSource userTokenSource = new CancellationTokenSource(userTokenExpiry))
            {
                watch.Start();
                policy.Invoking(p => p.Execute(
                    ct => {
                        SystemClock.Sleep(TimeSpan.FromSeconds(timeout), ct);  // Simulate cancel in the middle of execution
                        return ResultPrimitive.WhateverButTooLate;
                    }, userTokenSource.Token) // ... with user token.
                   ).ShouldThrow<OperationCanceledException>();
                watch.Stop();
            }

            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(timeout * 0.8));
            watch.Elapsed.Should().BeCloseTo(userTokenExpiry, ((int)tolerance.TotalMilliseconds));

        }

        [Fact]
        public void Should_not_execute_user_delegate_if_user_cancellationtoken_cancelled_before_delegate_reached__optimistic()
        {
            var policy = Policy.Timeout<ResultPrimitive>(10, TimeoutStrategy.Optimistic);

            bool executed = false;

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();

                policy.Invoking(p => p.Execute(ct =>
                {
                    executed = true;
                    return ResultPrimitive.WhateverButTooLate;
                }, cts.Token))
                .ShouldThrow<OperationCanceledException>();
            }

            executed.Should().BeFalse();
        }

        #endregion

        #region onTimeout overload - pessimistic

        [Fact]
        public void Should_call_ontimeout_with_configured_timeout__pessimistic()
        {
            TimeSpan timeoutPassedToConfiguration = TimeSpan.FromMilliseconds(250);

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };

            var policy = Policy.Timeout<ResultPrimitive>(timeoutPassedToConfiguration, TimeoutStrategy.Pessimistic, onTimeout);

            policy.Invoking(p => p.Execute(() =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                return ResultPrimitive.WhateverButTooLate;
            }))
            .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutPassedToConfiguration);
        }

        [Fact]
        public void Should_call_ontimeout_with_passed_context__pessimistic()
        {
            string executionKey = Guid.NewGuid().ToString();
            Context contextPassedToExecute = new Context(executionKey);

            Context contextPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { contextPassedToOnTimeout = ctx; };

            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Pessimistic, onTimeout);

            policy.Invoking(p => p.Execute(() =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                    return ResultPrimitive.WhateverButTooLate;
                }, contextPassedToExecute))
                .ShouldThrow<TimeoutRejectedException>();

            contextPassedToOnTimeout.Should().NotBeNull();
            contextPassedToOnTimeout.ExecutionKey.Should().Be(executionKey);
            contextPassedToOnTimeout.Should().BeSameAs(contextPassedToExecute);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Should_call_ontimeout_with_timeout_supplied_different_for_each_execution_by_evaluating_func__pessimistic(int programaticallyControlledDelay)
        {

            Func<TimeSpan> timeoutFunc = () => TimeSpan.FromMilliseconds(25*programaticallyControlledDelay);

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };

            var policy = Policy.Timeout<ResultPrimitive>(timeoutFunc, TimeoutStrategy.Pessimistic, onTimeout);

            policy.Invoking(p => p.Execute(() =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                    return ResultPrimitive.WhateverButTooLate;
                }))
                .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutFunc());

        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Should_call_ontimeout_with_timeout_supplied_different_for_each_execution_by_evaluating_func_influenced_by_context__pessimistic(int programaticallyControlledDelay)
        {
            Func<Context, TimeSpan> timeoutProvider = ctx => (TimeSpan)ctx["timeout"];

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };
            var policy = Policy.Timeout<ResultPrimitive>(timeoutProvider, TimeoutStrategy.Pessimistic, onTimeout);

            // Supply a programatically-controlled timeout, via the execution context.
            Context context = new Context("SomeExecutionKey") { ["timeout"] = TimeSpan.FromMilliseconds(25 * programaticallyControlledDelay) };

            policy.Invoking(p => p.Execute(() =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                    return ResultPrimitive.WhateverButTooLate;
                }, context))
                .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutProvider(context));
        }

        [Fact]
        public void Should_call_ontimeout_with_task_wrapping_abandoned_action__pessimistic()
        {
            Task taskPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { taskPassedToOnTimeout = task; };

            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Pessimistic, onTimeout);

            policy.Invoking(p => p.Execute(() =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(3), CancellationToken.None);
                return ResultPrimitive.WhateverButTooLate;
            }))
            .ShouldThrow<TimeoutRejectedException>();

            taskPassedToOnTimeout.Should().NotBeNull();
        }

        [Fact]
        public void Should_call_ontimeout_with_task_wrapping_abandoned_action_allowing_capture_of_otherwise_unobserved_exception__pessimistic()
        {
            Exception exceptionToThrow = new DivideByZeroException();

            Exception exceptionObservedFromTaskPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) =>
            {
                task.ContinueWith(t => exceptionObservedFromTaskPassedToOnTimeout = t.Exception.InnerException);
            };

            TimeSpan shimTimespan = TimeSpan.FromSeconds(1); // Consider increasing shimTimeSpan if test fails transiently in different environments.
            TimeSpan thriceShimTimeSpan = shimTimespan + shimTimespan + shimTimespan;
            var policy = Policy.Timeout<ResultPrimitive>(shimTimespan, TimeoutStrategy.Pessimistic, onTimeout);

            policy.Invoking(p => p.Execute(() =>
            {
                SystemClock.Sleep(thriceShimTimeSpan, CancellationToken.None);
                throw exceptionToThrow;
            }))
            .ShouldThrow<TimeoutRejectedException>();

            SystemClock.Sleep(thriceShimTimeSpan, CancellationToken.None);
            exceptionObservedFromTaskPassedToOnTimeout.Should().NotBeNull();
            exceptionObservedFromTaskPassedToOnTimeout.Should().Be(exceptionToThrow);

        }

        #endregion


        #region onTimeout overload - optimistic

        [Fact]
        public void Should_call_ontimeout_with_configured_timeout__optimistic()
        {
            TimeSpan timeoutPassedToConfiguration = TimeSpan.FromMilliseconds(250);

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };

            var policy = Policy.Timeout<ResultPrimitive>(timeoutPassedToConfiguration, TimeoutStrategy.Optimistic, onTimeout);
            var userCancellationToken = CancellationToken.None;

            policy.Invoking(p => p.Execute(ct =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(1), ct);
                return ResultPrimitive.WhateverButTooLate;
            }, userCancellationToken))
            .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutPassedToConfiguration);
        }

        [Fact]
        public void Should_call_ontimeout_with_passed_context__optimistic()
        {
            string executionKey = Guid.NewGuid().ToString();
            Context contextPassedToExecute = new Context(executionKey);

            Context contextPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { contextPassedToOnTimeout = ctx; };

            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Optimistic, onTimeout);
            var userCancellationToken = CancellationToken.None;

            policy.Invoking(p => p.Execute(ct =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), ct);
                    return ResultPrimitive.WhateverButTooLate;
                }, contextPassedToExecute, userCancellationToken))
                .ShouldThrow<TimeoutRejectedException>();

            contextPassedToOnTimeout.Should().NotBeNull();
            contextPassedToOnTimeout.ExecutionKey.Should().Be(executionKey);
            contextPassedToOnTimeout.Should().BeSameAs(contextPassedToExecute);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Should_call_ontimeout_with_timeout_supplied_different_for_each_execution_by_evaluating_func__optimistic(int programaticallyControlledDelay)
        {
            Func<TimeSpan> timeoutFunc = () => TimeSpan.FromMilliseconds(25*programaticallyControlledDelay);

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };

            var policy = Policy.Timeout<ResultPrimitive>(timeoutFunc, TimeoutStrategy.Optimistic, onTimeout);
            var userCancellationToken = CancellationToken.None;

            policy.Invoking(p => p.Execute(ct =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), ct);
                    return ResultPrimitive.WhateverButTooLate;
                }, userCancellationToken))
                .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutFunc());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Should_call_ontimeout_with_timeout_supplied_different_for_each_execution_by_evaluating_func_influenced_by_context__optimistic(int programaticallyControlledDelay)
        {
            Func<Context, TimeSpan> timeoutProvider = ctx => (TimeSpan)ctx["timeout"];

            TimeSpan? timeoutPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { timeoutPassedToOnTimeout = span; };
            var policy = Policy.Timeout<ResultPrimitive>(timeoutProvider, TimeoutStrategy.Optimistic, onTimeout);
            var userCancellationToken = CancellationToken.None;

            // Supply a programatically-controlled timeout, via the execution context.
            Context context = new Context("SomeExecutionKey")
            {
                ["timeout"] = TimeSpan.FromMilliseconds(25 * programaticallyControlledDelay)
            };

            policy.Invoking(p => p.Execute(ct =>
                {
                    SystemClock.Sleep(TimeSpan.FromSeconds(3), ct);
                    return ResultPrimitive.WhateverButTooLate;
                }, context, userCancellationToken))
                .ShouldThrow<TimeoutRejectedException>();

            timeoutPassedToOnTimeout.Should().Be(timeoutProvider(context));
        }

        [Fact]
        public void Should_call_ontimeout_but_not_with_task_wrapping_abandoned_action__optimistic()
        {
            Task taskPassedToOnTimeout = null;
            Action<Context, TimeSpan, Task> onTimeout = (ctx, span, task) => { taskPassedToOnTimeout = task; };

            TimeSpan timeout = TimeSpan.FromMilliseconds(250);
            var policy = Policy.Timeout<ResultPrimitive>(timeout, TimeoutStrategy.Optimistic, onTimeout);
            var userCancellationToken = CancellationToken.None;

            policy.Invoking(p => p.Execute(ct =>
            {
                SystemClock.Sleep(TimeSpan.FromSeconds(3), ct);
                return ResultPrimitive.WhateverButTooLate;
            }, userCancellationToken))
            .ShouldThrow<TimeoutRejectedException>();

            taskPassedToOnTimeout.Should().BeNull();
        }

        #endregion

    }
}