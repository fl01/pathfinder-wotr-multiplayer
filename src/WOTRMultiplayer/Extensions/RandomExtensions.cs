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
        /// RulebookEvent.D
        /// </summary>
        /// <param name="formula"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        public static int Roll(this DiceFormula formula, Random random)
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
        /// LinqExtensions.WeightedRandom
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

        /// <summary>
        /// LinqExtensions.Shuffle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="random"></param>
        public static void Shuffle<T>(this IList<T> list, Random random)
        {
            int i = list.Count;
            while (i > 1)
            {
                int num = random.Next(0, i) % i;
                i--;
                T t = list[num];
                list[num] = list[i];
                list[i] = t;
            }
        }
    }
}
