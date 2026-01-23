using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.RuleSystem;
using Kingmaker.Utility;

namespace WOTRMultiplayer.Extensions
{
    public static class RandomExtensions
    {
        public static float NextFloat(this Random random, float minInclusive, float maxExclusive)
        {
            var result = minInclusive + (float)random.NextDouble() * (maxExclusive - minInclusive);
            return result;
        }

        /// <summary>
        /// copy-paste of RulebookEvent.D with configurable random
        /// </summary>
        /// <param name="formula"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        public static float Run(this DiceFormula formula, Random random)
        {
            var rolls = formula.Rolls;
            var dice = formula.Dice;

            int num = 0;
            while (rolls-- > 0)
            {
                int num2 = random.Next(1, dice.Sides() + 1);
                num += num2;
            }

            return num;
        }

        /// <summary>
        /// copy-paste of LinqExtensions.WeightedRandom with configurable random
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        public static T WeightedRandom<T>(this IList<T> list, Random random) where T : IWeighted
        {
            if (list.Count <= 0)
            {
                return default;
            }

            float maxInclusive = list.Sum(x => x.Weight);
            float num = random.NextFloat(0f, maxInclusive);
            float num2 = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                T result = list[i];
                num2 += result.Weight;
                if (num2 >= num)
                {
                    return result;
                }
            }

            return list[list.Count - 1];
        }
    }
}
