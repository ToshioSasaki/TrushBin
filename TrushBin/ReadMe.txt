■TrushBinについて■

━概要━
このアプリケーションはファイルとディレクトリを完全に削除するものです。
使い方はいたって簡単Windowsのゴミ箱と同じ使い方でOKです！
ただし、削除したファイルは基本的に復旧できません。
※まだ試していませんが、復旧ソフトでも復旧できないと思います。

━使い方━
グラデーションのごみ箱アイコンを左クリックすると
どうする
・はい：空にする・・・・・・完全に削除されます。一切復旧はできません。
・いいえ：フォルダを開く・・基本的にc:\trushbin\ディレクトリにした
　　　　　　　　　　　　　　ファイルが入ります。
　　　　　　　　　　　　　　ここのファイルの一覧が表示されます。
・キャンセル：閉じる・・・・何も処理をせずに閉じます。

━削除フォルダの場所━
・c:\trushbin\
　に基本的に永久的に削除するファイルが入ります。

━セットアップ━
　このソースフォルダを展開してください。TrushBin.SlnをVisualStudioで開きます。
　以下の手順でセットアップを行ってください。
　※基本的ソースファイルを展開しているドライブを元にセットファイルを行います。
　１．Releaseにてビルドする
　２．C:\Users\toshi>g:
　３．G:\>cd G:\PROJECT\00complete\TrushBin\TrushBin\TrushBin\bin\Release\net8.0-windows
　４．G:\PROJECT\00complete\TrushBin\TrushBin\TrushBin\bin\Release\net8.0-windows>TrushBin.exe --setup
　５．これにてデスクトップに「ゴミ箱(Trush)」が作成されます。