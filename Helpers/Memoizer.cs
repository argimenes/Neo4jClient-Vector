using log4net;
using Neo4jClientVector.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Neo4jClientVector.Helpers
{
    public static class Memoizer
    {
        private static readonly ILog Log = LogManager.GetLogger("DefaultLogger");
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private static readonly MemoryCache _cache = MemoryCache.Default;

        // protected readonly bool EnableCache = bool.Parse(ApplicationConfig.AppSettings["EnableCache"]);
        static bool EnableCache = true;

        public static async Task<T> GetOrSetAsync<T>(Expression<Func<Task<T>>> populatorExpression, TimeSpan expire, object parameters = null)
        {
            var populator = GetFuncAsync(populatorExpression);
            var key = GetMethodFullNameAsync(populatorExpression);
            return await GetOrSetAsync(key, populator, expire, parameters);
        }

        // <summary>
        /// Get funky.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        static Func<T> GetFunc<T>(Expression<Func<T>> expression)
        {
            Func<T> func = expression.Compile();
            return func;
        }

        static Func<Task<T>> GetFuncAsync<T>(Expression<Func<Task<T>>> expression)
        {
            Func<Task<T>> func = expression.Compile();
            return func;
        }

        /// <summary>
        /// Gets the full name of the method referenced in the lambda expression.
        /// E.g, if () => atelierService.Search is passed in it will return something like "IAtelierService.Search".
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        static string GetMethodFullName<T>(Expression<Func<T>> expression)
        {
            return GetNameFromExpression(expression.Body);
        }

        static string GetMethodFullNameAsync<T>(Expression<Func<Task<T>>> expression)
        {
            return GetFullNameFromExpression(expression.Body);
        }

        static string GetMethodName<T>(Expression<Func<T>> expression)
        {
            return GetNameFromExpression(expression.Body);
        }

        static string GetNameFromExpression(Expression expressionBody)
        {
            var method = ((MethodCallExpression)expressionBody).Method;
            return method.Name;
        }

        static string GetFullNameFromExpression(Expression expressionBody)
        {
            var method = ((MethodCallExpression)expressionBody).Method;
            var className = method.DeclaringType.FullName;
            var methodName = method.Name;
            var key = className + "." + methodName;
            return key;
        }

        public static async Task ResetAsync<T>(Expression<Func<Task<T>>> populatorExpression, object parameters = null)
        {
            if (false == EnableCache)
            {
                return;
            }
            try
            {
                var populator = GetFuncAsync(populatorExpression);
                var key = GetMethodFullNameAsync(populatorExpression);
                var parametersJson = parameters == null ? "" : JsonConvert.SerializeObject(parameters);
                var keyWithParameters = key + parametersJson;
                var item = _cache.Remove(keyWithParameters);
                Log.Debug(new
                {
                    Message = "Removed item from cache",
                    ItemFound = (item != null),
                    T = typeof(T),
                    keyWithParameters
                });
            }
            catch (Exception ex)
            {
                Log.Error(new { T = typeof(T), parameters }, ex);
            }
        }

        public static void Reset<T>(Expression<Func<T>> populatorExpression, object parameters = null)
        {
            if (false == EnableCache)
            {
                return;
            }
            try
            {
                var populator = GetFunc(populatorExpression);
                var key = GetMethodFullName(populatorExpression);
                var parametersJson = parameters == null ? "" : JsonConvert.SerializeObject(parameters);
                var keyWithParameters = key + parametersJson;
                var item = _cache.Remove(keyWithParameters);
                Log.Debug(new
                {
                    Message = "Removed item from cache",
                    ItemFound = item != null,
                    KeyWithParameters = keyWithParameters
                });
            }
            catch (Exception ex)
            {
                Log.Error(new
                {
                    Message = "Unhandled exception",
                    T = typeof(T),
                    Parameters = parameters
                }, ex);
            }
        }

        public static T GetOrSet<T>(Expression<Func<T>> populatorExpression, TimeSpan expire, object parameters = null)
        {
            var populator = GetFunc(populatorExpression);
            var key = GetMethodFullName(populatorExpression);
            return GetOrSet(key, populator, expire, parameters);
        }

        public static T GetOrSet<T>(string key, Func<T> populator, TimeSpan expire, object parameters = null)
        {
            if (false == EnableCache)
            {
                return populator();
            }
            var parametersJson = parameters == null ? "" : JsonConvert.SerializeObject(parameters);
            var keyWithParameters = key + parametersJson;
            if (false == _cache.Contains(keyWithParameters))
            {
                var item = populator();
                if (ViolatesCachePolicy(item))
                {
                    return item;
                }
                _cache.Add(keyWithParameters, item, DateTimeOffset.Now.Add(expire));
                Log.Debug(new
                {
                    Message = "Added item to cache",
                    Method = "CacheService.GetOrSet<T>",
                    ExpireTotalMinutes = expire.TotalMinutes,
                    KeyWithParameters = keyWithParameters
                });
            }
            return (T)_cache.Get(keyWithParameters);
        }

        /// <summary>
        /// Commonsense checks to see if we should bypass the caching of the populator result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        static bool ViolatesCachePolicy<T>(T item)
        {
            if (IsNullOrDefault(item))
            {
                return true;
            }
            var result = item as Result;
            if (result != null)
            {
                if (result.Unsuccessful())
                {
                    return true;
                }
                var resultT = item as Result<T>;
                if (IsNullOrDefault(resultT))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsNullOrDefault<T>(T item)
        {
            return EqualityComparer<T>.Default.Equals(item, default(T));
        }

        /// <summary>
        /// Used when the cache contents are returned asychronously.
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/31831860/async-threadsafe-get-from-memorycache"/>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="populator"></param>
        /// <param name="expire"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> populator, TimeSpan expire, object parameters = null)
        {
            if (false == EnableCache)
            {
                return await populator();
            }
            if (parameters != null)
            {
                key += JsonConvert.SerializeObject(parameters);
            }
            if (false == _cache.Contains(key))
            {
                await semaphoreSlim.WaitAsync();
                try
                {
                    if (!_cache.Contains(key))
                    {
                        var data = await populator();
                        if (data != null)
                        {
                            _cache.Add(key, data, DateTimeOffset.Now.Add(expire));
                        }
                    }
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            }
            return (T)_cache.Get(key);
        }
    }
}
