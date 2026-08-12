using UnityEngine;
using UnityEngine.UI;

namespace WOTRMultiplayer.UI.Graphics
{
    public class StarIcon : Graphic
    {
        public float OuterRadius { get; private set; }

        public float InnerRadius { get; private set; }

        public int Points { get; private set; }

        public StarIcon WithDimensions(int points, float innerRadius, float outerRadius)
        {
            Points = points;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;

            return this;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var center = rectTransform.rect.center;
            var centerVertex = UIVertex.simpleVert;
            centerVertex.color = color;
            centerVertex.position = center;
            vertexHelper.AddVert(centerVertex);

            for (var i = 0; i < Points * 2; i++)
            {
                var angle = Mathf.PI * 2f * i / (Points * 2f) - Mathf.PI / 2f;

                var vertex = UIVertex.simpleVert;
                vertex.color = color;

                var radius = i % 2 == 0 ? OuterRadius : InnerRadius;
                vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertexHelper.AddVert(vertex);

                var next = (i + 1) % (Points * 2);
                vertexHelper.AddTriangle(0, i + 1, next + 1);
            }
        }
    }
}
