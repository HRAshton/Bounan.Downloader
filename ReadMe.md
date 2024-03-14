# BoUnAn Downloader

.NET Core worker service to copy anime from LoanApi to Telegram.

## Description

This is a part of the BoUnAn project.

## Getting Started

NB: You need to deploy Bounan.Bot`s stack first to create the SQS queue.

1. Clone the repository
2. Create environment
   1. Open terminal in AwsCdk
   2. Set an email in config.ts
   3. Run `npm install`
   4. Run `npm run deploy -- --profile <profile>`
   5. Keep the output of the command
3. Fill .env file
4. Run `docker-compose up`
