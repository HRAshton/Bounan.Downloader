#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { BounanDownloaderCdkStack } from '../lib/bounan-downloader-cdk-stack';

const app = new cdk.App();
new BounanDownloaderCdkStack(app, 'bounan-downloader');