﻿using Amazon.CDK;
using Bounan.Downloader.AwsCdk;

var app = new App();
_ = new AniManCdkStack(app, "Bounan-Downloader", new StackProps());
app.Synth();