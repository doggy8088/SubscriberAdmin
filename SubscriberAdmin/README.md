# SubscriberAdmin

範例專案使用方式：

1. 先安裝 [.NET 6 SDK](https://dot.net/download) 工具並下載原始碼

    ```sh
    git clone https://github.com/doggy8088/SubscriberAdmin.git -b answer
    cd SubscriberAdmin
    dotnet build -c Release
    cd SubscriberAdmin

    code .
    ```

2. 到 [LINE Developers Console](https://developers.line.biz/console/) 註冊 LINE Login 應用程式 (OAuth 2.0 Client)

    取得 `Channel ID` (`client_id`) 與 `Channel secret` (`client_secret`)

    註冊 `Callback URL` 為 `https://localhost:7133/signin-callback`

3. 到 [管理登錄服務 (服務提供者用)](https://notify-bot.line.me/my/services/) 註冊 LINE Notify 應用程式 (OAuth 2.0 Client)

    取得 `Client ID` (`client_id`) 與 `Client Secret` (`client_secret`)

    註冊 `Callback URL` 為 `https://localhost:7133/subscribe-callback`

4. 開啟 `appsettings.json` 填入兩組 `client_id` 與 `client_secret` 應用程式設定

    ```json
    {
      "LINELoginHandler": {
        "client_id": "",
        "client_secret": "",
        "redirect_uri": "/signin-callback",
        "scope": "profile openid email",
        "authURL": "https://access.line.me/oauth2/v2.1/authorize",
        "tokenURL": "https://api.line.me/oauth2/v2.1/token",
        "revokeURL": "https://api.line.me/oauth2/v2.1/revoke",
        "profileURL": "https://api.line.me/v2/profile"
      },
      "LINENotifyHandler": {
        "client_id": "",
        "client_secret": "",
        "redirect_uri": "/subscribe-callback",
        "scope": "notify",
        "authURL": "https://notify-bot.line.me/oauth/authorize",
        "tokenURL": "https://notify-bot.line.me/oauth/token",
        "revokeURL": "https://notify-api.line.me/api/revoke",
        "notifyURL": "https://notify-api.line.me/api/notify"
      }
    }
    ```

5. 執行網站

    ```sh
    dotnet watch run
    ```

6. 登入 LINE Login

    <https://localhost:7133/signin>

    登入並授權後會回到 `https://localhost:7133/signin-callback?code=<CODE>&state=<STATE>`

    > 我的程式有透過 Session 寫入一個亂數的 `state` 值，用來防止 CSRF 攻擊的狀況。

    開啟個人資訊頁會看到 `lineUserId`、`lineLoginAccessToken` 與 `lineLoginIDToken` 已經寫入資料

    <https://localhost:7133/my>

    開啟 `/claims` 頁面會看到目前登入使用者的 Claims 資訊

    <https://localhost:7133/claims>

    > 預設第一位註冊的使用者就是「管理者」身份，這個人才能查看所有訂戶資料與發送推播訊息給所有人！

7. 訂閱 LINE Notify 通知

    <https://localhost:7133/subscribe>

    登入並授權後會回到 `https://localhost:7133/subscribe-callback?code=<CODE>&state=<STATE>`

    開啟個人資訊頁會看到 `lineNotifyAccessToken` 已經寫入資料！

    <https://localhost:7133/my>

8. 發送 LINE Notify 通知給所有訂戶

    <https://localhost:7133/notifyall>

    登入並授權後會回到 `https://localhost:7133/subscribe-callback?code=<CODE>&state=<STATE>`

    所有人會收到 `Hello LINENotify!` 的通知訊息！

9. 取消訂閱 LINE Notify 通知

    <https://localhost:7133/unsubscribe>

    你 LINE 中的 LINE Notify 官方帳號會出現 `與「SubscriberAdmin」的連動已解除。` 訊息。

    開啟個人資訊頁會看到 `lineNotifyAccessToken` 已經被清空資料！

    <https://localhost:7133/my>

10. 登出網站

    <https://localhost:7133/signout>

    你的 LINE Login 的 Access Token 將會被撤銷，且資料庫也會清除此人的 `lineLoginAccessToken` 與 `lineLoginIDToken` 欄位，但會保留 `lineUserId` 欄位，用以識別下次登入是否為同一人。

11. 再次用 LINE Login 登入

    <https://localhost:7133/signin>

    登入並授權後會回到 `https://localhost:7133/signin-callback?code=<CODE>&state=<STATE>`

    開啟所有訂戶資料頁面會看到，我們的程式不會建立新的帳號，因為我們用 LINE 的 UserId 檢查此人是否已經註冊過會員：

    <https://localhost:7133/all>

## 參考資訊

- [ASP.NET Core 6 Minimal APIs overview | Microsoft Docs](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [如何實作沒有 ASP.NET Core Identity 的 Cookie-based 身分驗證機制](https://blog.miniasp.com/post/2019/12/25/asp-net-core-3-cookie-based-authentication)
