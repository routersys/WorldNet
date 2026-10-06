#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <algorithm>
#include <chrono>
#include "audioio.h"
#include "prof.h"
#include "world/cheaptrick.h"
#include "world/d4c.h"
#include "world/dio.h"
#include "world/stonemask.h"
#include "world/synthesis.h"
double g_prof[32];
int main(int argc, char **argv) {
  int x_length = GetAudioLength(argv[1]);
  double *x = new double[x_length];
  int fs, nbit;
  wavread(argv[1], &fs, &nbit, x);
  const double frame_period = 5.0;
  DioOption dio_option; InitializeDioOption(&dio_option); dio_option.frame_period = frame_period;
  int f0_length = GetSamplesForDIO(fs, x_length, frame_period);
  double *tp = new double[f0_length], *f0 = new double[f0_length], *rf0 = new double[f0_length];
  Dio(x, x_length, fs, &dio_option, tp, f0);
  StoneMask(x, x_length, fs, tp, f0, f0_length, rf0);
  CheapTrickOption ct; InitializeCheapTrickOption(fs, &ct);
  int fft_size = ct.fft_size, sl = fft_size / 2 + 1;
  double **sp = new double *[f0_length], **ap = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i) { sp[i] = new double[sl]; ap[i] = new double[sl]; }
  CheapTrick(x, x_length, fs, tp, rf0, f0_length, &ct, sp);
  D4COption d4c; InitializeD4COption(&d4c);
  D4C(x, x_length, fs, tp, rf0, f0_length, fft_size, &d4c, ap);
  int y_length = (int)((f0_length - 1) * frame_period / 1000.0 * fs) + 1;
  double *y = new double[y_length];
  double best = 1e30; double bestp[32] = {0};
  for (int r = 0; r < 30; ++r) {
    memset(g_prof, 0, sizeof(g_prof));
    auto a = std::chrono::steady_clock::now();
    Synthesis(rf0, f0_length, sp, ap, fft_size, frame_period, fs, y_length, y);
    auto b = std::chrono::steady_clock::now();
    double ms = std::chrono::duration<double, std::milli>(b - a).count();
    if (ms < best) { best = ms; memcpy(bestp, g_prof, sizeof(bestp)); }
  }
  printf("Synthesis total %.3f ms\n", best);
  const char *names[] = {"env+ratio", "per.log", "per.minphase", "per.shiftloop", "per.ifft+shift+dc", "ap.noise(randn+fft)", "ap.log", "ap.minphase", "ap.mult", "ap.ifft+shift", "final mix", "accumulate", "", "", "mp.mirror", "mp.ifft(r2c)", "mp.cepstrum", "mp.fft(c2c)", "mp.exp/sincos"};
  for (int i = 0; i < 19; ++i) if (names[i][0]) printf("  %-22s %8.3f ms\n", names[i], bestp[i] / 1000.0);
  return 0;
}
