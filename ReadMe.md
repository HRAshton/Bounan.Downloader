# BoUnAn Downloader

.NET Core worker service to copy anime from LoanApi to Telegram.

## Description

This is a part of the BoUnAn project.

Tasks:
- Listen to SQS for signed URLs from BoUnAn Bot
- Download the file from the signed URL to the local storage
- Send the file to the Telegram bot
- Delete the file from the local storage
- Repeat
- Profit
- (Optional) Send the logs to CloudWatch
