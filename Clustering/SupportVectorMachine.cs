﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Algebra;

namespace Clustering {
    /// <summary>線形サポートベクタマシン</summary>
    public class LinearSupportVectorMachine : SupportVectorMachine {
        
        /// <summary>コンストラクタ</summary>
        /// <param name="cost">誤識別に対するペナルティの大きさ</param>
        public LinearSupportVectorMachine(double cost) : base(cost){ }

        /// <summary>カーネル関数</summary>
        protected override double Kernel(Vector vector1, Vector vector2) {
            double sum = 0;
            for(int i = 0; i < VectorDim; i++) {
                sum += vector1[i] * vector2[i];
            }

            return sum;
        }
    }
    
    /// <summary>ガウシアンサポートベクタマシン</summary>
    public class GaussianSupportVectorMachine : SupportVectorMachine {
        private double sigma, gamma;

        /// <param name="cost">誤識別に対するペナルティの大きさ</param>
        /// <param name="sigma">ガウシアン関数の尺度パラメータ</param>
        public GaussianSupportVectorMachine(double cost, double sigma) : base(cost){
            this.Sigma = sigma;
        }
        
        /// <summary>ガウシアン関数の尺度パラメータ</summary>
        public double Sigma {
            get {
                return sigma;
            }
            protected set {
                sigma = value;
                gamma = 1 / (2 * sigma * sigma);
            }
        }

        /// <summary>カーネル関数</summary>
        protected override double Kernel(Vector vector1, Vector vector2) {
            double norm = 0;
            for(int i = 0; i < vector1.Dim; i++) {
                double d = vector1[i] - vector2[i];
                norm += d * d;
            }

            return Math.Exp(-gamma * norm);
        }
    }
    
    /// <summary>サポートベクタマシン</summary>
    public abstract class SupportVectorMachine : IClusteringMethod {
        protected class WeightVector {
            public Vector Vector { get; set; }
            public double Weight { get; set; }
        }

        private double bias;
        private readonly double cost;
        List<WeightVector> support_vectors;

        /// <summary>コンストラクタ</summary>
        /// <param name="cost">誤識別に対するペナルティの大きさ</param>
        public SupportVectorMachine(double cost) {
            if(!(cost > 0)) {
                throw new ArgumentException(nameof(cost));
            }

            Initialize();
            this.cost = cost;
        }
        
        /// <summary>データクラス数</summary>
        /// <remarks>SVMは常に2</remarks>
        public int GroupCount => 2;

        /// <summary>ベクトルの次元数</summary>
        public int VectorDim {
            get; private set;
        }

        /// <summary>単一サンプルを分類</summary>
        /// <param name="vector">サンプルベクタ</param>
        public int Classify(Vector vector) {
            return ClassifyRaw(vector) > 0 ? +1 : ClassifyRaw(vector) < 0 ? -1 : 0;
        }

        /// <summary>単一サンプルを分類</summary>
        /// <param name="vector">サンプルベクタ</param>
        /// <param name="threshold">弁別しきい値</param>
        public int Classify(Vector vector, double threshold) {
            return ClassifyRaw(vector) > threshold ? +1 : ClassifyRaw(vector) < -threshold ? -1 : 0;
        }

        /// <summary>複数サンプルを分類</summary>
        /// <param name="vectors">サンプルベクタ集合</param>
        public IEnumerable<int> Classify(IEnumerable<Vector> vectors) {
            return vectors.Select((vector) => Classify(vector));
        }

        /// <summary>複数サンプルを分類</summary>
        /// <param name="vectors">サンプルベクタ集合</param>
        /// <param name="threshold">弁別しきい値</param>
        public IEnumerable<int> Classify(IEnumerable<Vector> vectors, double threshold) {
            return vectors.Select((vector) => Classify(vector, threshold));
        }
        
        /// <summary>単一サンプルの識別値</summary>
        public double ClassifyRaw(Vector vector) {
            if(vector == null || vector.Dim != VectorDim) {
                throw new ArgumentException();
            }

            double s = -bias;
            foreach(var support_vector in support_vectors) {
                s += support_vector.Weight * Kernel(vector, support_vector.Vector);
            }
            return s;
        }
        
        /// <summary>複数サンプルの識別値</summary>
        public IEnumerable<double> ClassifyRaw(IEnumerable<Vector> vectors) {
            return vectors.Select((vector) => ClassifyRaw(vector));
        }

        /// <summary>学習</summary>
        /// <param name="vector_dim">サンプルベクタ次元数</param>
        /// <param name="vectors_groups">データクラスごとのサンプルベクタ集合</param>
        /// <remarks>サンプルベクタ集合は正例と負例の2つ</remarks>
        public void Learn(int vector_dim, params List<Vector>[] vectors_groups) {
            Initialize();
            ValidateSample(vector_dim, vectors_groups);

            // サポートベクターとなる最小のベクトル重み
            double epsilon = 1.0e-3;

            // ベクトルの次元数
            VectorDim = vector_dim;

            // ベクトル
            List<Vector> positive_vectors = vectors_groups[0];
            List<Vector> negative_vectors = vectors_groups[1];
            List<Vector> inputs = new List<Vector>();
            inputs.AddRange(positive_vectors);
            inputs.AddRange(negative_vectors);

            // ラベル
            double[] outputs = (new double[inputs.Count]).Select((_, i) => i < positive_vectors.Count ? +1.0 : -1.0).ToArray();

            // 逐次最小問題最適化法実行
            var smo = new SequentialMinimalOptimization(inputs.ToArray(), outputs, cost, Kernel);
            smo.Optimize();

            bias = smo.Bias;
            ReadOnlyCollection<double> vector_weight = smo.VectorWeight;
            
            //サポートベクターの格納
            for(int i = 0; i < vector_weight.Count; i++) {
                if(vector_weight[i] > epsilon) {
                    var wvec = new WeightVector { Weight = vector_weight[i] * outputs[i], Vector = (Vector)inputs[i].Clone() };

                    support_vectors.Add(wvec);
                }
            }
        }

        /// <summary>カーネル関数</summary>
        protected abstract double Kernel(Vector vector1, Vector vector2);

        /// <summary>初期化</summary>
        public void Initialize() {
            bias = 0;
            support_vectors = new List<WeightVector>();
        }

        /// <summary>サンプルの正当性を検証</summary>
        private void ValidateSample(int vector_dim, List<Vector>[] vectors_groups) {
            if(vector_dim < 1) {
                throw new ArgumentException(nameof(vector_dim));
            }
            if(vectors_groups == null) {
                throw new ArgumentNullException(nameof(vectors_groups));
            }
            if(vectors_groups.Length != GroupCount) {
                throw new ArgumentException(nameof(vectors_groups));
            }
            foreach(var vectors in vectors_groups) {
                if(vectors.Count < 1) {
                    throw new ArgumentException(nameof(vectors_groups));
                }
                foreach(var vector in vectors) {
                    if(vector.Dim != vector_dim) {
                        throw new ArgumentException(nameof(vectors_groups));
                    }
                }
            }
        }
    }
}
