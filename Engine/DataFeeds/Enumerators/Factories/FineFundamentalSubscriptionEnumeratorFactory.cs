/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Provides an implementation of <see cref="ISubscriptionEnumeratorFactory"/> that reads
    /// an entire <see cref="SubscriptionDataSource"/> into a single <see cref="FineFundamental"/>
    /// to be emitted on the tradable date at midnight
    /// </summary>
    public class FineFundamentalSubscriptionEnumeratorFactory : ISubscriptionEnumeratorFactory
    {
        // creating a fine fundamental instance is expensive (its massive) so we keep our factory instance
        private static readonly FineFundamental FineFundamental = new FineFundamental();

        private readonly bool _isLiveMode;
        private readonly Func<SubscriptionRequest, IEnumerable<DateTime>> _tradableDaysProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FineFundamentalSubscriptionEnumeratorFactory"/> class.
        /// </summary>
        /// <param name="isLiveMode">True for live mode, false otherwise</param>
        /// <param name="tradableDaysProvider">Function used to provide the tradable dates to the enumerator.
        /// Specify null to default to <see cref="SubscriptionRequest.TradableDaysInDataTimeZone"/></param>
        public FineFundamentalSubscriptionEnumeratorFactory(bool isLiveMode, Func<SubscriptionRequest, IEnumerable<DateTime>> tradableDaysProvider = null)
        {
            _isLiveMode = isLiveMode;
            _tradableDaysProvider = tradableDaysProvider ?? (request => request.TradableDaysInDataTimeZone);
        }

        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <param name="dataProvider">Provider used to get data when it is not present on disk</param>
        /// <returns>An enumerator reading the subscription request</returns>
        public IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request, IDataProvider dataProvider)
        {
            using (var dataCacheProvider = new SingleEntryDataCacheProvider(dataProvider))
            {
                var tradableDays = _tradableDaysProvider(request);

                var fineFundamentalConfiguration = new SubscriptionDataConfig(request.Configuration, typeof(FineFundamental), request.Security.Symbol);

                foreach (var date in tradableDays)
                {
                    var fineFundamentalSource = FineFundamental.GetSource(fineFundamentalConfiguration, date, _isLiveMode);
                    var fineFundamentalFactory = SubscriptionDataSourceReader.ForSource(fineFundamentalSource, dataCacheProvider, fineFundamentalConfiguration, date, _isLiveMode, FineFundamental, dataProvider);
                    var fineFundamentalForDate = (FineFundamental)fineFundamentalFactory.Read(fineFundamentalSource).FirstOrDefault();

                    // directly do not emit null points. Null points won't happen when used with Coarse data since we are pre filtering based on Coarse.HasFundamentalData
                    // but could happen when fine filtering custom universes
                    if (fineFundamentalForDate != null)
                    {
                        yield return fineFundamentalForDate;
                    }
                }
            }
        }
    }
}
