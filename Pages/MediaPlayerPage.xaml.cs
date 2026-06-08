using System.Security.Cryptography;
using System.Text;
using NMU_CE_App.Services;

namespace NMU_CE_App.Pages;

public partial class MediaPlayerPage : ContentPage
{
    private string _fileUrl = "";
    private string _fileName = "";
    private bool _isAudio;
    private string _title = "";

    public MediaPlayerPage(string url, string name, bool isAudio, string title)
    {
        InitializeComponent();
        _fileUrl = Uri.UnescapeDataString(url ?? "");
        _fileName = Uri.UnescapeDataString(name ?? "");
        _isAudio = isAudio;
        _title = Uri.UnescapeDataString(title ?? "");
        PageTitle.Text = _title;
        PlayerWebView.Navigating += OnWebViewNavigating;
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("nmu://back") == true)
        {
            e.Cancel = true;
            await HandleBack();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(_fileUrl))
            LoadPlayer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SavePosition();
        PlayerWebView.Source = "about:blank";
    }

    private void LoadPlayer()
    {
        var safeTitle = _title.Replace("\\", "\\\\").Replace("'", "\\'");
        var safeVideoUrl = _fileUrl.Replace("\\", "\\\\").Replace("'", "\\'");
        var isAudioMode = _isAudio ? "true" : "false";
        var savedPos = GetPlaybackPosition(_fileUrl);
        var resumeTime = savedPos > 5 ? savedPos.ToString("F1") : "0";

        var html = BuildPlayerHtml(safeVideoUrl, safeTitle, isAudioMode, resumeTime);
        PlayerWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private void SavePosition()
    {
        try
        {
            PlayerWebView.EvaluateJavaScriptAsync(
                "(function(){ var v = document.getElementById('v'); return v ? v.currentTime.toString() : '0'; })()")
                .ContinueWith(t =>
                {
                    if (t.Result is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pos) && pos > 1)
                        SavePlaybackPosition(_fileUrl, pos);
                });
        }
        catch { }
    }

    private static string BuildPlayerHtml(string videoSrc, string title, string isAudioMode, string resumeTime)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no,viewport-fit=cover"">
<style>
:root {{
  --cyan:#00F2FF; --red:#FF0055; --green:#00FF88; --purple:#BD00FF;
  --bg:#000; --ctrl-bg:rgba(0,0,0,0.85); --glass:rgba(255,255,255,0.08);
}}
*{{margin:0;padding:0;box-sizing:border-box;-webkit-tap-highlight-color:transparent}}
html,body{{width:100%;height:100%;overflow:hidden;background:var(--bg);font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;color:#fff;touch-action:none;user-select:none;-webkit-user-select:none}}
body{{display:flex;flex-direction:column;height:100vh;height:100dvh}}

/* ===== Audio Background ===== */
.audio-bg{{position:absolute;inset:0;background:radial-gradient(ellipse at 30% 20%,#1a0b2e 0%,#0d0520 40%,#000 100%);display:none;flex-direction:column;align-items:center;justify-content:center;z-index:0}}
.audio-bg.on{{display:flex}}
.audio-note{{font-size:90px;filter:drop-shadow(0 0 40px var(--purple)) drop-shadow(0 0 80px rgba(189,0,255,0.3))}}
.audio-bars{{display:flex;gap:6px;height:60px;align-items:center;margin-top:30px}}
.ab{{width:5px;background:var(--purple);border-radius:3px;animation:abounce 0.8s ease-in-out infinite}}
.ab:nth-child(2){{animation-delay:.1s}}.ab:nth-child(3){{animation-delay:.2s}}.ab:nth-child(4){{animation-delay:.3s}}.ab:nth-child(5){{animation-delay:.4s}}.ab:nth-child(6){{animation-delay:.5s}}
@keyframes abounce{{0%,100%{{height:8px;opacity:.4}}50%{{height:32px;opacity:1}}}}
@keyframes spin{{to{{transform:rotate(360deg)}}}}

/* ===== Player ===== */
#player{{flex:1;position:relative;display:flex;align-items:center;justify-content:center;background:var(--bg);overflow:hidden}}
video{{width:100%;height:100%;object-fit:contain;background:#000}}
.audio-mode video{{display:none}}

/* ===== Controls Overlay ===== */
#ov{{position:absolute;inset:0;display:flex;flex-direction:column;justify-content:space-between;z-index:10;transition:opacity .35s ease;pointer-events:none}}
#ov>*{{pointer-events:auto}}
#ov.hide{{opacity:0;pointer-events:none!important}}

/* -- Top Bar -- */
.top{{padding:env(safe-area-inset-top,12px) 16px 12px;background:linear-gradient(to bottom,rgba(0,0,0,0.85) 0%,rgba(0,0,0,0.4) 70%,transparent 100%);display:flex;align-items:center;gap:12px}}
.top .back{{width:36px;height:36px;background:var(--glass);border-radius:50%;display:flex;align-items:center;justify-content:center;border:1px solid rgba(255,255,255,0.1);font-size:18px;cursor:pointer;flex-shrink:0;transition:background .2s}}
.top .back:active{{background:var(--cyan);color:#000}}
.top .ttl{{flex:1;font-size:14px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;opacity:.95;text-align:center}}
.top .badge{{font-size:10px;padding:3px 8px;border-radius:8px;font-weight:700;letter-spacing:.5px;flex-shrink:0;background:rgba(0,242,255,0.15);color:var(--cyan);border:1px solid rgba(0,242,255,0.25)}}

/* -- Center Tap Area -- */
.tap-area{{flex:1;display:flex;position:relative;pointer-events:auto}}

/* -- Seek Indicator -- */
.seek-ind{{position:absolute;top:50%;transform:translateY(-50%);width:70px;height:70px;background:rgba(0,0,0,0.55);border-radius:50%;display:flex;flex-direction:column;align-items:center;justify-content:center;opacity:0;transition:opacity .2s;pointer-events:none;backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px)}}
.seek-ind.left{{left:20%}}.seek-ind.right{{right:20%}}
.seek-ind.show{{opacity:1}}
.seek-ind .si-icon{{font-size:20px}}.seek-ind .si-txt{{font-size:10px;font-weight:700;margin-top:2px;opacity:.8}}

/* -- Center Play Button (big) -- */
#bigPlay{{position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);width:64px;height:64px;background:rgba(0,0,0,0.5);border:2px solid rgba(255,255,255,0.2);border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:28px;cursor:pointer;backdrop-filter:blur(8px);-webkit-backdrop-filter:blur(8px);transition:transform .2s,opacity .2s;opacity:0;pointer-events:none}}
#bigPlay.show{{opacity:1;pointer-events:auto}}
#bigPlay:active{{transform:translate(-50%,-50%) scale(0.9)}}

/* -- Bottom Controls -- */
.bot{{padding:0 16px env(safe-area-inset-bottom,16px);background:linear-gradient(to top,rgba(0,0,0,0.92) 0%,rgba(0,0,0,0.6) 60%,transparent 100%)}}

/* -- Progress -- */
.pwrap{{position:relative;width:100%;height:28px;display:flex;align-items:center;cursor:pointer;touch-action:none}}
.ptrack{{width:100%;height:4px;background:rgba(255,255,255,0.18);border-radius:4px;position:relative;overflow:visible}}
.pbuf{{height:100%;background:rgba(255,255,255,0.12);border-radius:4px;position:absolute;left:0;top:0;width:0%}}
.pfill{{height:100%;background:var(--cyan);border-radius:4px;position:absolute;left:0;top:0;width:0%;box-shadow:0 0 8px rgba(0,242,255,0.4)}}
.pthumb{{width:14px;height:14px;background:#fff;border-radius:50%;position:absolute;top:50%;transform:translate(-50%,-50%);left:0%;box-shadow:0 0 6px rgba(0,0,0,0.4);opacity:0;transition:opacity .15s;pointer-events:none}}
.pwrap.drag .pthumb{{opacity:1}}
.pwrap.drag .ptrack{{height:6px}}
.pwrap .ptime{{position:absolute;top:-22px;background:rgba(0,0,0,0.8);padding:2px 6px;border-radius:4px;font-size:11px;font-family:monospace;white-space:nowrap;transform:translateX(-50%);display:none;pointer-events:none}}
.pwrap.drag .ptime{{display:block}}

/* -- Buttons Row -- */
.brow{{display:flex;align-items:center;justify-content:space-between;padding:4px 0 8px}}
.bleft,.bright{{display:flex;align-items:center;gap:16px}}
.bbtn{{background:none;border:none;color:#fff;font-size:18px;cursor:pointer;opacity:.8;transition:opacity .15s,padding .1s;padding:6px;border-radius:8px;-webkit-tap-highlight-color:transparent}}
.bbtn:active{{opacity:1;transform:scale(0.92)}}
.pbtn{{font-size:22px;width:40px;height:40px;display:flex;align-items:center;justify-content:center;background:var(--glass);border-radius:50%;border:1px solid rgba(255,255,255,0.1)}}
.pbtn:active{{background:var(--cyan);color:#000}}
.tdisp{{font-family:'SF Mono',Monaco,'Cascadia Code',monospace;font-size:12px;letter-spacing:.5px;opacity:.85;min-width:90px}}
.spd{{font-family:monospace;font-size:13px;background:var(--glass);border:1px solid rgba(255,255,255,0.12);border-radius:6px;padding:3px 10px;cursor:pointer;transition:background .2s}}
.spd:active{{background:var(--cyan);color:#000}}
.vol-wrap{{display:flex;align-items:center;gap:6px}}
.vol-icon{{font-size:14px;opacity:.6;cursor:pointer}}
input[type=range]{{width:55px;height:3px;accent-color:var(--cyan);background:transparent;cursor:pointer}}

/* -- Buffering Spinner -- */
#spinner{{position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);width:44px;height:44px;border:3px solid transparent;border-top-color:var(--cyan);border-bottom-color:var(--cyan);border-radius:50%;animation:spin .8s linear infinite;display:none;z-index:5}}
#spinner.on{{display:block}}

/* -- Error -- */
#err{{position:absolute;inset:0;display:none;flex-direction:column;align-items:center;justify-content:center;gap:12px;background:rgba(0,0,0,0.85);z-index:20}}
#err.on{{display:flex}}
#err .ei{{font-size:48px;opacity:.6}}#err .et{{font-size:15px;font-weight:600}}#err .es{{font-size:12px;opacity:.5;text-align:center;padding:0 20px}}

/* -- Resume Banner -- */
#rbanner{{position:absolute;bottom:90px;left:50%;transform:translateX(-50%);background:rgba(0,242,255,0.12);border:1px solid rgba(0,242,255,0.25);border-radius:10px;padding:6px 14px;font-size:12px;color:var(--cyan);font-weight:600;display:none;z-index:10;backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px)}}

/* -- Speed Popup -- */
#spopup{{position:absolute;bottom:80px;right:16px;background:rgba(15,23,42,0.95);border:1px solid rgba(255,255,255,0.1);border-radius:12px;padding:6px;display:none;z-index:15;backdrop-filter:blur(10px);-webkit-backdrop-filter:blur(10px)}}
#spopup.on{{display:flex;flex-direction:column;gap:2px}}
.sopt{{padding:8px 20px;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;transition:background .15s;text-align:center}}
.sopt:active{{background:var(--cyan);color:#000}}
.sopt.cur{{color:var(--cyan)}}
</style>
</head>
<body>
<div id=""player"">
  <div class=""audio-bg"" id=""abg""><div class=""audio-note"">🎵</div><div class=""audio-bars""><div class=""ab""></div><div class=""ab""></div><div class=""ab""></div><div class=""ab""></div><div class=""ab""></div><div class=""ab""></div></div></div>
  <video id=""v"" playsinline preload=""auto"" webkit-playsinline></video>
  <div id=""spinner""></div>
  <div id=""err""><div class=""ei"">⚠️</div><div class=""et"">Unable to Play</div><div class=""es"">Check your internet connection and try again.</div></div>
  <div id=""rbanner""></div>
  <div id=""bigPlay"">▶</div>

  <div id=""ov"">
    <div class=""top"">
      <div class=""back"" onclick=""goBack()"">←</div>
      <div class=""ttl"" id=""ttl""></div>
      <div class=""badge"">LIVE</div>
    </div>

    <div class=""tap-area"" id=""tapArea"">
      <div class=""seek-ind left"" id=""sL""><div class=""si-icon"">⏪</div><div class=""si-txt"">10s</div></div>
      <div class=""seek-ind right"" id=""sR""><div class=""si-icon"">⏩</div><div class=""si-txt"">10s</div></div>
    </div>

    <div class=""bot"">
      <div class=""pwrap"" id=""pwrap"">
        <div class=""ptrack""><div class=""pbuf"" id=""pbuf""></div><div class=""pfill"" id=""pfill""></div></div>
        <div class=""pthumb"" id=""pthumb""></div>
        <div class=""ptime"" id=""ptime"">0:00</div>
      </div>
      <div class=""brow"">
        <div class=""bleft"">
          <button class=""bbtn pbtn"" id=""playBtn"" onclick=""togglePlay()"">▶</button>
          <div class=""tdisp""><span id=""ct"">0:00</span> <span style=""opacity:.4"">/</span> <span id=""dur"">0:00</span></div>
          <div class=""vol-wrap"">
            <span class=""vol-icon"" id=""volIcon"" onclick=""toggleMute()"">🔊</span>
            <input type=""range"" id=""volR"" min=""0"" max=""1"" step=""0.05"" value=""1"" oninput=""setVol(this.value)"" />
          </div>
        </div>
        <div class=""bright"">
          <button class=""bbtn"" onclick=""skip(-10)"">⏪</button>
          <button class=""bbtn"" onclick=""skip(10)"">⏩</button>
          <span class=""spd"" id=""spdBtn"" onclick=""toggleSpdPopup()"">1x</span>
        </div>
      </div>
    </div>
  </div>
  <div id=""spopup""></div>
</div>
<script>
(function(){{
  var v=document.getElementById('v'),
      ov=document.getElementById('ov'),
      pfill=document.getElementById('pfill'),
      pbuf=document.getElementById('pbuf'),
      pthumb=document.getElementById('pthumb'),
      pwrap=document.getElementById('pwrap'),
      ptime=document.getElementById('ptime'),
      spinner=document.getElementById('spinner'),
      err=document.getElementById('err'),
      rbanner=document.getElementById('rbanner'),
      bigPlay=document.getElementById('bigPlay'),
      playBtn=document.getElementById('playBtn'),
      spdBtn=document.getElementById('spdBtn'),
      spopup=document.getElementById('spopup'),
      volIcon=document.getElementById('volIcon'),
      volR=document.getElementById('volR'),
      tapArea=document.getElementById('tapArea'),
      sL=document.getElementById('sL'),
      sR=document.getElementById('sR'),
      ttl=document.getElementById('ttl');

  var isAudio={isAudioMode},
      resumeAt={resumeTime},
      speeds=[0.5,0.75,1,1.25,1.5,2,2.5,3],
      curSpdIdx=2,
      paused=true,
      hasErr=false,
      idleTimer,
      lastDragPct=0,
      seekingL=false,
      seekingR=false,
      seekLTmr,
      seekRTmr;

  /* --- Init --- */
  v.src='{videoSrc}';
  ttl.textContent='{title}';
  document.title='{title}';
  if(isAudio){{document.getElementById('abg').classList.add('on');document.body.classList.add('audio-mode')}}
  v.load();

  /* --- Resume --- */
  v.addEventListener('loadedmetadata',function(){{
    updateDuration();
    if(resumeAt>5&&v.duration&&v.duration>resumeAt){{
      v.currentTime=resumeAt;
      rbanner.textContent='▶ Resuming from '+fmt(resumeAt);
      rbanner.style.display='block';
      setTimeout(function(){{rbanner.style.display='none'}},2500);
    }}
    if(v.paused&&!hasErr)bigPlay.classList.add('show');
  }});

  /* --- Playback Events --- */
  v.addEventListener('play',function(){{paused=false;playBtn.textContent='⏸';bigPlay.classList.remove('show');resetIdle()}});
  v.addEventListener('pause',function(){{paused=true;playBtn.textContent='▶';bigPlay.classList.add('show');showControls()}});
  v.addEventListener('ended',function(){{paused=true;playBtn.textContent='▶';bigPlay.classList.add('show');showControls()}});

  /* --- Buffering --- */
  v.addEventListener('waiting',function(){{if(!hasErr)spinner.classList.add('on')}});
  v.addEventListener('canplay',function(){{spinner.classList.remove('on');err.classList.remove('on');hasErr=false}});
  v.addEventListener('playing',function(){{spinner.classList.remove('on');err.classList.remove('on');hasErr=false}});
  v.addEventListener('loadstart',function(){{if(!hasErr)spinner.classList.add('on')}});
  v.addEventListener('error',function(){{
    spinner.classList.remove('on');
    hasErr=true;
    err.classList.add('on');
    bigPlay.classList.remove('show');
  }});

  /* --- Progress (smooth via rAF) --- */
  var rafId=null;
  function updateProgress(){{
    if(v.duration&&isFinite(v.duration)){{
      var pct=(v.currentTime/v.duration)*100;
      pfill.style.width=pct+'%';
      pthumb.style.left=pct+'%';
      document.getElementById('ct').textContent=fmt(v.currentTime);
      if(!pwrap.classList.contains('drag')){{
        pthumb.style.opacity='0';
      }}
    }}
    if(v.buffered.length>0){{
      var buffEnd=v.buffered.end(v.buffered.length-1);
      pbuf.style.width=(buffEnd/v.duration*100)+'%';
    }}
    rafId=requestAnimationFrame(updateProgress);
  }}
  function startRaf(){{if(!rafId)rafId=requestAnimationFrame(updateProgress)}}
  function stopRaf(){{if(rafId){{cancelAnimationFrame(rafId);rafId=null}}}}
  v.addEventListener('play',startRaf);
  v.addEventListener('pause',function(){{stopRaf();updateProgress()}});
  v.addEventListener('ended',function(){{stopRaf();updateProgress()}});

  function updateDuration(){{
    if(v.duration&&isFinite(v.duration))document.getElementById('dur').textContent=fmt(v.duration);
  }}
  v.addEventListener('loadedmetadata',updateDuration);
  v.addEventListener('durationchange',updateDuration);

  /* --- Controls Visibility --- */
  function showControls(){{ov.classList.remove('hide');resetIdle()}}
  function hideControls(){{if(!paused)ov.classList.add('hide')}}
  function resetIdle(){{clearTimeout(idleTimer);idleTimer=setTimeout(hideControls,4000)}}
  ['mousemove','touchstart'].forEach(function(e){{tapArea.addEventListener(e,showControls)}});

  /* --- Big Play / Tap --- */
  var lastTap=0;
  tapArea.addEventListener('click',function(e){{
    var now=Date.now();
    if(now-lastTap<300){{lastTap=0;return}}
    lastTap=now;
  }});
  bigPlay.addEventListener('click',function(e){{
    e.stopPropagation();
    togglePlay();
  }});

  /* --- Double Tap Seek --- */
  var lastTapL=0,lastTapR=0;
  sL.addEventListener('click',function(e){{
    e.stopPropagation();
    var now=Date.now();
    if(now-lastTapL<400){{clearTimeout(seekLTmr);skip(-10);showSeekInd(sL)}}
    else{{seekLTmr=setTimeout(function(){{skip(-10);showSeekInd(sL)}},350)}}
    lastTapL=now;showControls();
  }});
  sR.addEventListener('click',function(e){{
    e.stopPropagation();
    var now=Date.now();
    if(now-lastTapR<400){{clearTimeout(seekRTmr);skip(10);showSeekInd(sR)}}
    else{{seekRTmr=setTimeout(function(){{skip(10);showSeekInd(sR)}},350)}}
    lastTapR=now;showControls();
  }});
  function showSeekInd(el){{el.classList.add('show');setTimeout(function(){{el.classList.remove('show')}},300)}}

  /* --- Play/Pause --- */
  function togglePlay(){{
    if(v.paused){{v.play().catch(function(){{}})}}else{{v.pause()}}
  }}
  window.togglePlay=togglePlay;

  /* --- Skip --- */
  function skip(s){{v.currentTime=Math.max(0,Math.min(v.duration||0,v.currentTime+s))}}
  window.skip=skip;

  /* --- Speed --- */
  function buildSpdPopup(){{
    spopup.innerHTML='';
    for(var i=0;i<speeds.length;i++){{
      var d=document.createElement('div');
      d.className='sopt'+(i===curSpdIdx?' cur':'');
      d.textContent=speeds[i]+'x';
      d.setAttribute('data-idx',i);
      d.addEventListener('click',function(){{
        var idx=parseInt(this.getAttribute('data-idx'));
        curSpdIdx=idx;v.playbackRate=speeds[idx];spdBtn.textContent=speeds[idx]+'x';
        spopup.classList.remove('on');buildSpdPopup();
      }});
      spopup.appendChild(d);
    }}
  }}
  buildSpdPopup();
  function toggleSpdPopup(){{spopup.classList.toggle('on');showControls()}}
  window.toggleSpdPopup=toggleSpdPopup;
  document.addEventListener('click',function(e){{if(!spopup.contains(e.target)&&e.target!==spdBtn)spopup.classList.remove('on')}});

  /* --- Volume --- */
  function setVol(val){{v.volume=val;updateVolIcon()}}
  window.setVol=setVol;
  function updateVolIcon(){{volIcon.textContent=v.muted||v.volume===0?'🔇':v.volume<0.3?'🔈':v.volume<0.7?'🔉':'🔊'}}
  function toggleMute(){{v.muted=!v.muted;updateVolIcon()}}
  window.toggleMute=toggleMute;
  volR.addEventListener('input',function(){{v.muted=false;updateVolIcon()}});

  /* --- Fullscreen --- */
  function toggleFullscreen(){{
    var el=document.getElementById('player');
    if(!document.fullscreenElement)el.requestFullscreen().catch(function(){{}});
    else document.exitFullscreen();
  }}
  window.toggleFullscreen=toggleFullscreen;

  /* --- Go Back --- */
  function goBack(){{window.location.href='nmu://back'}}
  window.goBack=goBack;

  /* --- Progress Drag --- */
  var dragPct=0;
  function getDragPct(e){{
    var rect=pwrap.getBoundingClientRect();
    var x=e.touches?e.touches[0].clientX:e.clientX;
    return Math.max(0,Math.min(1,(x-rect.left)/rect.width));
  }}
  function updateDragUI(pct){{
    var pp=pct*100;
    pfill.style.width=pp+'%';
    pthumb.style.left=pp+'%';
    pthumb.style.opacity='1';
    ptime.textContent=fmt(pct*(v.duration||0));
    ptime.style.left=pp+'%';
  }}
  pwrap.addEventListener('mousedown',function(e){{pwrap.classList.add('drag');dragPct=getDragPct(e);updateDragUI(dragPct);showControls()}});
  pwrap.addEventListener('touchstart',function(e){{pwrap.classList.add('drag');dragPct=getDragPct(e);updateDragUI(dragPct);showControls()}},{{passive:true}});
  window.addEventListener('mousemove',function(e){{if(pwrap.classList.contains('drag')){{dragPct=getDragPct(e);updateDragUI(dragPct)}}}});
  window.addEventListener('touchmove',function(e){{if(pwrap.classList.contains('drag')){{dragPct=getDragPct(e);updateDragUI(dragPct)}}}},{{passive:true}});
  function endDrag(){{
    if(!pwrap.classList.contains('drag'))return;
    pwrap.classList.remove('drag');
    if(v.duration)v.currentTime=dragPct*v.duration;
    setTimeout(function(){{pthumb.style.opacity='0'}},300);
  }}
  window.addEventListener('mouseup',endDrag);
  window.addEventListener('touchend',endDrag);

  /* --- Init state --- */
  spinner.classList.add('on');
  showControls();
}})();
</script>
</body>
</html>";
    }

    private async Task HandleBack()
    {
        try
        {
            var result = await PlayerWebView.EvaluateJavaScriptAsync(
                "(function(){ var v = document.getElementById('v'); return v ? v.currentTime.toString() : '0'; })()");
            if (double.TryParse(result, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pos) && pos > 1)
                SavePlaybackPosition(_fileUrl, pos);
        }
        catch { }

        PlayerWebView.Source = "about:blank";
        NavHelper.Back(this);
    }

    private void OnTitleBarBack(object? sender, EventArgs e)
    {
        _ = HandleBack();
    }

    private static string GetPosKey(string url)
    {
        return $"vidpos_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16]}";
    }

    private static void SavePlaybackPosition(string url, double seconds)
    {
        Preferences.Set(GetPosKey(url), seconds);
    }

    private static double GetPlaybackPosition(string url)
    {
        return Preferences.Get(GetPosKey(url), 0.0);
    }
}
