using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blish_HUD.Entities;
using Blish_HUD.Pathing.Trails;
using CSCore.Streams.SampleConverter;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.MediaFoundation;

namespace Blish_HUD.Pathing.Entities {
    public class ScrollingTrailSection : Trail, ITrail {

        #region Load Static

        private static readonly Effect _basicTrailEffect;
        private static readonly Texture2D _trailCloudTexture;

        static ScrollingTrailSection() {
            _basicTrailEffect = BlishHud.ActiveContentManager.Load<Effect>("effects\\trail");
            _trailCloudTexture = GameService.Content.GetTexture("uniformclouds_blur30");
        }

        #endregion

        private float _animationSpeed = 1;

        private float _fadeNear = 10000;
        private float _fadeFar  = 10000;
        private float _scale    = 1;
        private bool _fadeCenter = true;
        private float _playerFadeRadius = 0.25f;

        private VertexPositionColorTexture[] VertexData { get; set; }

        private VertexBuffer _vertexBuffer;

        public float AnimationSpeed {
            get => _animationSpeed;
            set => SetProperty(ref _animationSpeed, value);
        }

        public override Texture2D TrailTexture {
            get => _trailTexture;
            set {
                if (SetProperty(ref _trailTexture, value))
                    Console.WriteLine("TextureSet");
                    //InitTrailPoints();
            }
        }

        public float FadeNear {
            get => _fadeNear;
            set => SetProperty(ref _fadeNear, value);
        }

        public float FadeFar {
            get => _fadeFar;
            set => SetProperty(ref _fadeFar, value);
        }

        public float Scale {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public bool FadeCenter {
            get => _fadeCenter;
            set => SetProperty(ref _fadeCenter, value);
        }

        public float PlayerFadeRadius {
            get => _playerFadeRadius;
            set => SetProperty(ref _playerFadeRadius, value);
        }

        public ScrollingTrailSection() : base(null) { /* NOOP */ }

        public ScrollingTrailSection(List<Vector3> trailPoints) : base(trailPoints) { /* NOOP */ }

        private List<Vector3> SetTrailResolution(List<Vector3> trailPoints, float pointResolution) {
            List<Vector3> tempTrail = new List<Vector3>();

            var lstPoint = trailPoints[0];

            for (int i = 0; i < trailPoints.Count; i++) {
                var dist = Vector3.Distance(lstPoint, trailPoints[i]);

                var s   = dist / pointResolution;
                var inc = 1    / s;

                for (float v = inc; v < s - inc; v += inc) {
                    var nPoint = Vector3.Lerp(lstPoint, _trailPoints[i], v / s);

                    tempTrail.Add(nPoint);
                }

                tempTrail.Add(trailPoints[i]);

                lstPoint = trailPoints[i];
            }

            return tempTrail;
        }

        private float SplineLength(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1) {

            Vector3 c0 = m0;
            Vector3 c1 = 6f * (p1 - p0) - 4f * m0 - 2f * m1;
            Vector3 c2 = 6f * (p0 - p1) + 3f * (m1 + m0);

            Func<float, Vector3> derivative = t => c0 + t * (c1 + t * c2);

            List<Vector2> GaussLegendreCoefficients = new List<Vector2>() {
              new Vector2(0.0f, 0.5688889f),
              new Vector2( -0.5384693f, 0.47862867f ),
              new Vector2(0.5384693f, 0.47862867f ),
              new Vector2( -0.90617985f, 0.23692688f ),
              new Vector2( 0.90617985f, 0.23692688f )
            };

            float length = 0.0f;

            foreach (var coeff in GaussLegendreCoefficients) {
                float t = 0.5f * (1.0f + coeff.X);
                length += derivative(t).Length() * coeff.Y;
            }
            return 0.5f * length;

        }

        private List<Vector3> CreateHermiteTrail() {
            List<Vector3> hermitePoints = new List<Vector3>();
            float alpha = 0.5f;

            //Hermite basis functions
            Func<float, float> h00 = t => (1 + 2 * t) * (float) Math.Pow(1 - t, 2.0f);
            Func<float, float> h10 = t => t * (float) Math.Pow(1 - t, 2.0f);
            Func<float, float> h01 = t => (float) Math.Pow(t, 2.0f) * (3 - 2 * t);
            Func<float, float> h11 = t => (float) Math.Pow(t, 2.0f) * (t - 1);

            Vector3 p0, p1, m0, m1;

            float GetCurvature(float t0) {
                //First derivative
                Func<float, float> h00dt = t => 6 * t * t - 6 * t;
                Func<float, float> h10dt = t => 3 * t * t - 4 * t + 1;
                Func<float, float> h01dt = t => -6 * t * t + 6 * t;
                Func<float, float> h11dt = t => 3 * t * t - 2*t;

                //Second derivative
                Func<float, float> h00dt2 = t => 12 * t - 6;
                Func<float, float> h10dt2 = t => 6 * t - 4;
                Func<float, float> h01dt2 = t => -12 * t + 6;
                Func<float, float> h11dt2 = t => 6 * t - 2;

                var curvature = (float) (Vector3.Cross(h00dt(t0) * p0 + h10dt(t0) * m0 + h01dt(t0) * p1 + h11dt(t0) * m1,
                                              h00dt2(t0) * p0 + h10dt2(t0) * m0 + h01dt2(t0) * p1 + h11dt2(t0) * m1).Length()
                                              / Math.Pow((h00dt(t0) * p0 + h10dt(t0) * m0 + h01dt(t0) * p1 + h11dt(t0) * m1).Length(), 3));
                return curvature;
            }

            hermitePoints.Add(this.TrailPoints.First());

            for (int k = 0; k < this.TrailPoints.Count - 1; k++) {

                p0 = this.TrailPoints[k];
                p1 = this.TrailPoints[k + 1];

                if (k > 0)
                    m0 = alpha * (p1 - this.TrailPoints[k - 1]);
                else
                    m0 = (p1 - p0);

                if (k < this.TrailPoints.Count - 2)
                    m1 = alpha * (this.TrailPoints[k + 2] - p0);
                else
                    m1 = (p1 - p0);

                var numPoints = (uint) (SplineLength(p0, m0, p1, m1) / 0.15f);

                for (int i = 0; i < numPoints; i++) {
                    var t = i * (1.0f / numPoints);
                    var kappa = GetCurvature(t);
                    if (kappa < 0.015) continue;

                    hermitePoints.Add(h00(t) * p0 + h10(t) * m0 + h01(t) * p1 + h11(t) * m1);

                    if (kappa > 2) {
                        for (var n = 0.1f; n < 1; n += 0.1f) {
                            var t0 = t;
                            var t1 = (i + 1) * (1.0f / numPoints);

                            var dt = (t1 - t0) * n;

                            hermitePoints.Add(h00(t + dt) * p0 + h10(t + dt) * m0 + h01(t + dt) * p1 + h11(t + dt) * m1);
                        }
                    }
                }
            }

            //GameService.Overlay.QueueMainThreadUpdate((gameTime) => {
            //    foreach (var point in DrawThesePoints) {
            //        if ((point - GameService.Gw2Mumble.PlayerCharacter.Position).Length() < 25) {
            //            var leftBoxPoint = new Cube() {
            //                Color = Color.Red,
            //                Size = new Vector3(0.01f),
            //                Position = point
            //            };

            //            GameService.Graphics.World.Entities.Add(leftBoxPoint);
            //        }
            //    }
            //});

            hermitePoints.Add(this.TrailPoints.Last());
            return hermitePoints;
        }

        protected override void InitTrailPoints() {
            if (!_trailPoints.Any()) return;

            var temp = this.TrailPoints;

            //GameService.Overlay.QueueMainThreadUpdate((gameTime) => {
            //    foreach (var point in temp) {
            //        if ((point - GameService.Gw2Mumble.PlayerCharacter.Position).Length() < 25) {
            //            var leftBoxPoint = new Cube() {
            //                Color = Color.Red,
            //                Size = new Vector3(0.02f),
            //                Position = point
            //            };

            //            GameService.Graphics.World.Entities.Add(leftBoxPoint);
            //        }
            //    }
            //});

            // TacO has a minimum of 30, so we'll use 30

            //_trailPoints = _trailPoints.DouglasPeucker();

            //_trailPoints.Reverse();

            _trailPoints = SetTrailResolution(_trailPoints, 30f);

            _trailPoints = this.CreateHermiteTrail();

            //_trailPoints = _trailPoints.DouglasPeucker();

            //_trailPoints = SetTrailResolution(_trailPoints, 30f);

            this.VertexData = new VertexPositionColorTexture[this.TrailPoints.Count * 2];

            float imgScale = ScrollingTrail.TRAIL_WIDTH;

            float pastDistance = this.TrailLength;

            var offsetDirection = new Vector3(0, 0, -1);

            var currPoint = this.TrailPoints[0];
            Vector3 offset = Vector3.Zero;

            for (int i = 0; i < this.TrailPoints.Count - 1; i++) {

                if((currPoint - GameService.Gw2Mumble.PlayerCharacter.Position).Length() < 10) {
                    Console.WriteLine("Test");
                }
                    

                var nextPoint = this.TrailPoints[i + 1];
                Vector3 pathDirection;
                //if(i < this.TrailPoints.Count - 2)
                //    pathDirection = this.TrailPoints[i + 2] - this.TrailPoints[i + 1];//nextPoint - currPoint;
                //else
                //    pathDirection = this.TrailPoints[i+1] - this.TrailPoints[i];

                pathDirection = this.TrailPoints[i + 1] - this.TrailPoints[i];

                offset = Vector3.Cross(pathDirection, offsetDirection);

                offset.Normalize();

                var leftPoint = currPoint + (offset * imgScale);
                var rightPoint = currPoint + (offset * -imgScale);

                this.VertexData[i * 2 + 1] = new VertexPositionColorTexture(leftPoint, Color.White, new Vector2(0f, pastDistance / (imgScale * 2) - 1));
                this.VertexData[i * 2] = new VertexPositionColorTexture(rightPoint, Color.White, new Vector2(1f, pastDistance / (imgScale * 2) - 1));

                pastDistance -= Vector3.Distance(currPoint, nextPoint);

                currPoint = nextPoint;

#if PLOTTRAILS
                GameService.Overlay.QueueMainThreadUpdate((gameTime) => {
                    var leftBoxPoint = new Cube() {
                        Color = Color.Red,
                        Size = new Vector3(0.025f),
                        Position = this.TrailPoints[i+1]
                    };

                    var rightBoxPoint = new Cube() {
                        Color = Color.Red,
                        Size = new Vector3(0.05f),
                        Position = rightPoint
                    };

                    GameService.Graphics.World.Entities.Add(leftBoxPoint);
                    GameService.Graphics.World.Entities.Add(rightBoxPoint);
                });
#endif
            }

            GameService.Overlay.QueueMainThreadUpdate((gameTime) => {
                foreach (var point in this.TrailPoints) {
                    if ((point - GameService.Gw2Mumble.PlayerCharacter.Position).Length() < 25) {
                        var leftBoxPoint = new Cube() {
                            Color = Color.Red,
                            Size = new Vector3(0.005f),
                            Position = point
                        };

                        GameService.Graphics.World.Entities.Add(leftBoxPoint);
                    }
                }
            });

            var fleftPoint = currPoint + (offset * imgScale);
            var frightPoint = currPoint + (offset * -imgScale);

            this.VertexData[this.TrailPoints.Count * 2 - 1] = new VertexPositionColorTexture(fleftPoint, Color.White, new Vector2(0f, pastDistance / (imgScale * 2) - 1));
            this.VertexData[this.TrailPoints.Count * 2 - 2] = new VertexPositionColorTexture(frightPoint, Color.White, new Vector2(1f, pastDistance / (imgScale * 2) - 1));

            _vertexBuffer = new VertexBuffer(BlishHud.ActiveGraphicsDeviceManager.GraphicsDevice, VertexPositionColorTexture.VertexDeclaration, this.VertexData.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(this.VertexData);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            _basicTrailEffect.Parameters["TotalMilliseconds"].SetValue((float)gameTime.TotalGameTime.TotalMilliseconds);
        }

        public override void Draw(GraphicsDevice graphicsDevice) {
            if (this.TrailTexture == null || this.VertexData == null || this.VertexData.Length < 3) return;

            _basicTrailEffect.Parameters["CameraPosition"].SetValue(GameService.Gw2Mumble.PlayerCamera.Position);
            _basicTrailEffect.Parameters["WorldViewProjection"].SetValue(GameService.Gw2Mumble.PlayerCamera.WorldViewProjection);
            _basicTrailEffect.Parameters["PlayerViewProjection"].SetValue(GameService.Gw2Mumble.PlayerCamera.PlayerView /** GameService.Gw2Mumble.PlayerCamera.Projection*/);
            _basicTrailEffect.Parameters["Texture"].SetValue(this.TrailTexture);
            _basicTrailEffect.Parameters["CloudTexture"].SetValue(_trailCloudTexture);
            _basicTrailEffect.Parameters["FlowSpeed"].SetValue(this.AnimationSpeed);
            _basicTrailEffect.Parameters["PlayerPosition"].SetValue(GameService.Gw2Mumble.PlayerCharacter.Position);
            _basicTrailEffect.Parameters["FadeNear"].SetValue(this.FadeNear);
            _basicTrailEffect.Parameters["FadeFar"].SetValue(this.FadeFar);
            _basicTrailEffect.Parameters["Opacity"].SetValue(this.Opacity);
            _basicTrailEffect.Parameters["FadeCenter"].SetValue(this.FadeCenter);
            _basicTrailEffect.Parameters["PlayerFadeRadius"].SetValue(this.PlayerFadeRadius);
            _basicTrailEffect.Parameters["TotalLength"].SetValue(20f);

            graphicsDevice.SetVertexBuffer(_vertexBuffer, 0);

            foreach (EffectPass trailPass in _basicTrailEffect.CurrentTechnique.Passes) {
                trailPass.Apply();

                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, this._vertexBuffer.VertexCount - 2);

                //graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip,
                //                                  this.VertexData,
                //                                  0,
                //                                  this.VertexData.Length - 2);
            }
        }

    }
}
