﻿using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Deform
{
	[Deformer (Name = "Skew", Description = "Skews mesh", Type = typeof (SkewDeformer))]
	public class SkewDeformer : Deformer, IFactor
	{
		public float Factor
		{
			get => factor;
			set => factor = value;
		}
		public BoundsMode Mode
		{
			get => mode;
			set => mode = value;
		}
		public float Top
		{
			get => top;
			set => top = Mathf.Max (value, bottom);
		}
		public float Bottom
		{
			get => bottom;
			set => bottom = Mathf.Min(value, top);
		}
		public Transform Axis
		{
			get
			{
				if (axis == null)
					axis = transform;
				return axis;
			}
			set => axis = value;
		}

		[SerializeField, HideInInspector] private float factor;
		[SerializeField, HideInInspector] private BoundsMode mode= BoundsMode.Unlimited;
		[SerializeField, HideInInspector] private float top = 0.5f;
		[SerializeField, HideInInspector] private float bottom = -0.5f;
		[SerializeField, HideInInspector] private Transform axis;

		public override DataFlags DataFlags => DataFlags.Vertices;

		public override JobHandle Process (MeshData data, JobHandle dependency = default (JobHandle))
		{
			if (Factor == 0f)
				return dependency;

			var meshToAxis = DeformerUtils.GetMeshToAxisSpace (Axis, data.Target.GetTransform ());

			switch (Mode)
			{
				default:
					return new UnlimitedSkewDeformJob
					{
						factor = Factor,
						meshToAxis = meshToAxis,
						axisToMesh = meshToAxis.inverse,
						vertices = data.DynamicNative.VertexBuffer
					}.Schedule (data.length, BatchCount, dependency);
				case BoundsMode.Limited:
					return new LimitedSkewDeformJob
					{
						factor = Factor,
						top = top,
						bottom = bottom,
						meshToAxis = meshToAxis,
						axisToMesh = meshToAxis.inverse,
						vertices = data.DynamicNative.VertexBuffer
					}.Schedule (data.length, BatchCount, dependency);
			}
		}

		[BurstCompile (CompileSynchronously = COMPILE_SYNCHRONOUSLY)]
		private struct UnlimitedSkewDeformJob : IJobParallelFor
		{
			public float factor;
			public float4x4 meshToAxis;
			public float4x4 axisToMesh;
			public NativeArray<float3> vertices;

			public void Execute (int index)
			{
				var point = mul (meshToAxis, float4 (vertices[index], 1f));

				point.z += point.y * factor;

				vertices[index] = mul (axisToMesh, point).xyz;
			}
		}
		[BurstCompile (CompileSynchronously = COMPILE_SYNCHRONOUSLY)]
		private struct LimitedSkewDeformJob : IJobParallelFor
		{
			public float factor;
			public float top;
			public float bottom;
			public float4x4 meshToAxis;
			public float4x4 axisToMesh;
			public NativeArray<float3> vertices;

			public void Execute (int index)
			{
				var point = mul (meshToAxis, float4 (vertices[index], 1f));

				var samplePoint = clamp (point.y, bottom, top);
				point.z += samplePoint * factor;

				vertices[index] = mul (axisToMesh, point).xyz;
			}
		}
	}
}