#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <chrono>
#include "audioio.h"
#include "prof.h"
#include "world/cheaptrick.h"
#include "world/dio.h"
#include "world/stonemask.h"
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
  double **sp = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i) sp[i] = new double[sl];
  double best = 1e30; double bestp[32] = {0};
  for (int r = 0; r < 30; ++r) {
    memset(g_prof, 0, sizeof(g_prof));
    auto a = std::chrono::steady_clock::now();
    CheapTrick(x, x_length, fs, tp, rf0, f0_length, &ct, sp);
    auto b = std::chrono::steady_clock::now();
    double ms = std::chrono::duration<double, std::milli>(b - a).count();
    if (ms < best) { best = ms; memcpy(bestp, g_prof, sizeof(bestp)); }
  }
  printf("CheapTrick total %.3f ms (frames %d)\n", best, f0_length);
  const char *names[] = {"setparams(cos,window)", "windowed(randn)+sums", "fft(power)", "power spectrum", "linear smoothing", "infinitesimal(randn)", "lifters(sin/cos)", "log loop", "fft fwd (cepstrum)", "lifter mult", "ifft", "exp loop", "", "dc correction"};
  for (int i = 0; i < 14; ++i) if (names[i][0]) printf("  %-22s %8.3f ms\n", names[i], bestp[i] / 1000.0);
  return 0;
}
