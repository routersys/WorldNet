#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "audioio.h"
#include "dump.h"
#include "world/cheaptrick.h"
#include "world/codec.h"
#include "world/common.h"
#include "world/constantnumbers.h"
#include "world/d4c.h"
#include "world/dio.h"
#include "world/fft.h"
#include "world/harvest.h"
#include "world/matlabfunctions.h"
#include "world/stonemask.h"
#include "world/synthesis.h"

char g_outdir[1024];

namespace {

const int kFftSizes[] = { 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };
const int kFftSizeCount = sizeof(kFftSizes) / sizeof(kFftSizes[0]);
const int kMaxFftSize = 4096;

uint32_t g_seed;

void ResetSeed() {
  g_seed = 2463534242u;
}

double NextSample() {
  g_seed ^= g_seed << 13;
  g_seed ^= g_seed >> 17;
  g_seed ^= g_seed << 5;
  return (double)g_seed / 4294967296.0 * 2.0 - 1.0;
}

void FillDeterministic(double *buffer, int length) {
  for (int i = 0; i < length; ++i) buffer[i] = NextSample();
}

void DumpFft() {
  double *real_input = new double[kMaxFftSize];
  ResetSeed();
  FillDeterministic(real_input, kMaxFftSize);
  Write1D("fft_real_input", real_input, kMaxFftSize);

  double *complex_input = new double[kMaxFftSize * 2];
  FillDeterministic(complex_input, kMaxFftSize * 2);
  Write2D("fft_complex_input", complex_input, kMaxFftSize, 2);

  char name[256];
  for (int s = 0; s < kFftSizeCount; ++s) {
    int n = kFftSizes[s];

    double *wave = new double[n];
    fft_complex *spectrum = new fft_complex[n];
    memcpy(wave, real_input, sizeof(double) * n);
    fft_plan r2c = fft_plan_dft_r2c_1d(n, wave, spectrum, FFT_ESTIMATE);
    fft_execute(r2c);
    snprintf(name, sizeof(name), "fft_r2c_%d", n);
    WriteComplex(name, spectrum, n / 2 + 1);
    fft_destroy_plan(r2c);

    fft_complex *c_in = new fft_complex[n];
    double *out_real = new double[n];
    for (int i = 0; i < n; ++i) {
      c_in[i][0] = complex_input[i * 2];
      c_in[i][1] = complex_input[i * 2 + 1];
    }
    fft_plan c2r = fft_plan_dft_c2r_1d(n, c_in, out_real, FFT_ESTIMATE);
    fft_execute(c2r);
    snprintf(name, sizeof(name), "fft_c2r_%d", n);
    Write1D(name, out_real, n);
    fft_destroy_plan(c2r);

    fft_complex *c_out = new fft_complex[n];
    for (int i = 0; i < n; ++i) {
      c_in[i][0] = complex_input[i * 2];
      c_in[i][1] = complex_input[i * 2 + 1];
    }
    fft_plan fwd = fft_plan_dft_1d(n, c_in, c_out, FFT_FORWARD, FFT_ESTIMATE);
    fft_execute(fwd);
    snprintf(name, sizeof(name), "fft_c2c_forward_%d", n);
    WriteComplex(name, c_out, n);
    fft_destroy_plan(fwd);

    for (int i = 0; i < n; ++i) {
      c_in[i][0] = complex_input[i * 2];
      c_in[i][1] = complex_input[i * 2 + 1];
    }
    fft_plan bwd = fft_plan_dft_1d(n, c_in, c_out, FFT_BACKWARD, FFT_ESTIMATE);
    fft_execute(bwd);
    snprintf(name, sizeof(name), "fft_c2c_backward_%d", n);
    WriteComplex(name, c_out, n);
    fft_destroy_plan(bwd);

    delete[] c_out;
    delete[] out_real;
    delete[] c_in;
    delete[] spectrum;
    delete[] wave;
  }

  delete[] complex_input;
  delete[] real_input;
}

void DumpMatlabRound() {
  const double inputs[] = {
    0.0, 0.5, -0.5, 0.49999999999999994, -0.49999999999999994,
    1.5, -1.5, 2.5, -2.5, 1.4999999999999998, -1.4999999999999998,
    0.1, -0.1, 0.9, -0.9, 123.456, -123.456, 1000000.5, -1000000.5,
    2147483.5, -2147483.5, 3.0, -3.0
  };
  const int count = sizeof(inputs) / sizeof(inputs[0]);
  double *results = new double[count];
  for (int i = 0; i < count; ++i) results[i] = (double)matlab_round(inputs[i]);
  Write1D("mf_round_input", inputs, count);
  Write1D("mf_round_output", results, count);
  delete[] results;
}

void DumpDecimate() {
  const int kNFact = 9;
  const int length = 1000;
  double *x = new double[length];
  ResetSeed();
  FillDeterministic(x, length);
  Write1D("mf_decimate_input", x, length);

  char name[256];
  for (int r = 2; r <= 12; ++r) {
    int nout = (length - 1) / r + 1;
    int nbeg = r - r * nout + length;
    int written = 0;
    for (int i = nbeg; i < length + kNFact; i += r) ++written;
    double *y = new double[length + 2 * kNFact];
    decimate(x, length, r, y);
    snprintf(name, sizeof(name), "mf_decimate_r%d", r);
    Write1D(name, y, written);
    delete[] y;
  }
  delete[] x;
}

void DumpInterpolation() {
  const int x_length = 100;
  const int xi_length = 397;
  double *x = new double[x_length];
  double *y = new double[x_length];
  double *xi = new double[xi_length];
  double *yi = new double[xi_length];

  ResetSeed();
  for (int i = 0; i < x_length; ++i) x[i] = i * 1.5;
  FillDeterministic(y, x_length);
  for (int i = 0; i < xi_length; ++i)
    xi[i] = x[0] + (x[x_length - 1] - x[0]) * i / (xi_length - 1.0);

  Write1D("mf_interp1_x", x, x_length);
  Write1D("mf_interp1_y", y, x_length);
  Write1D("mf_interp1_xi", xi, xi_length);
  interp1(x, y, x_length, xi, xi_length, yi);
  Write1D("mf_interp1_yi", yi, xi_length);

  int *index = new int[xi_length];
  for (int i = 0; i < xi_length; ++i) index[i] = 0;
  histc(x, x_length, xi, xi_length, index);
  WriteInts("mf_histc_index", index, xi_length);

  double *diff_out = new double[x_length - 1];
  diff(y, x_length, diff_out);
  Write1D("mf_diff_output", diff_out, x_length - 1);

  WriteScalar("mf_std_output", matlab_std(y, x_length));

  double *shifted = new double[x_length];
  fftshift(y, x_length, shifted);
  Write1D("mf_fftshift_output", shifted, x_length);

  const double origin = 0.0;
  const double shift = 1.5;
  double *xiq = new double[xi_length];
  double *yiq = new double[xi_length];
  for (int i = 0; i < xi_length; ++i)
    xiq[i] = origin + (x_length - 1) * shift * i / (xi_length - 1.0) * 0.999;
  interp1Q(origin, shift, y, x_length, xiq, xi_length, yiq);
  Write1D("mf_interp1q_xi", xiq, xi_length);
  Write1D("mf_interp1q_yi", yiq, xi_length);

  delete[] yiq;
  delete[] xiq;
  delete[] shifted;
  delete[] diff_out;
  delete[] index;
  delete[] yi;
  delete[] xi;
  delete[] y;
  delete[] x;
}

void DumpRandn() {
  const int count = 4096;
  RandnState state;
  randn_reseed(&state);
  double *values = new double[count];
  for (int i = 0; i < count; ++i) values[i] = randn(&state);
  Write1D("mf_randn_values", values, count);

  double final_state[4];
  final_state[0] = (double)state.g_randn_x;
  final_state[1] = (double)state.g_randn_y;
  final_state[2] = (double)state.g_randn_z;
  final_state[3] = (double)state.g_randn_w;
  Write1D("mf_randn_state", final_state, 4);
  delete[] values;
}

void DumpFastFftFilt() {
  const int x_length = 300;
  const int h_length = 64;
  const int fft_size = 1024;
  double *x = new double[x_length];
  double *h = new double[h_length];
  double *y = new double[fft_size];

  ResetSeed();
  FillDeterministic(x, x_length);
  FillDeterministic(h, h_length);

  ForwardRealFFT forward_real_fft;
  InitializeForwardRealFFT(fft_size, &forward_real_fft);
  InverseRealFFT inverse_real_fft;
  InitializeInverseRealFFT(fft_size, &inverse_real_fft);

  fast_fftfilt(x, x_length, h, h_length, fft_size, &forward_real_fft,
      &inverse_real_fft, y);

  Write1D("mf_fastfftfilt_x", x, x_length);
  Write1D("mf_fastfftfilt_h", h, h_length);
  Write1D("mf_fastfftfilt_y", y, fft_size);

  DestroyInverseRealFFT(&inverse_real_fft);
  DestroyForwardRealFFT(&forward_real_fft);
  delete[] y;
  delete[] h;
  delete[] x;
}

void DumpSuitableFftSize() {
  const int count = 8192;
  double *results = new double[count];
  for (int i = 0; i < count; ++i)
    results[i] = (double)GetSuitableFFTSize(i + 1);
  Write1D("cm_suitable_fft_size", results, count);
  delete[] results;
}

void DumpNuttallWindow() {
  const int lengths[] = { 2, 3, 8, 15, 64, 257, 1024 };
  const int count = sizeof(lengths) / sizeof(lengths[0]);
  char name[256];
  for (int i = 0; i < count; ++i) {
    double *y = new double[lengths[i]];
    NuttallWindow(lengths[i], y);
    snprintf(name, sizeof(name), "cm_nuttall_%d", lengths[i]);
    Write1D(name, y, lengths[i]);
    delete[] y;
  }
}

void DumpSpectrumHelpers() {
  const int fft_size = 2048;
  const int fs = 44100;
  const int spectrum_length = fft_size / 2 + 1;
  const double f0 = 200.0;

  double *power_spectrum = new double[spectrum_length];
  ResetSeed();
  for (int i = 0; i < spectrum_length; ++i)
    power_spectrum[i] = NextSample() * 0.5 + 1.5;
  Write1D("cm_power_spectrum", power_spectrum, spectrum_length);

  double *dc_output = new double[spectrum_length];
  memcpy(dc_output, power_spectrum, sizeof(double) * spectrum_length);
  DCCorrection(power_spectrum, f0, fs, fft_size, dc_output);
  Write1D("cm_dc_correction", dc_output, spectrum_length);

  double *smoothed = new double[spectrum_length];
  LinearSmoothing(power_spectrum, f0, fs, fft_size, smoothed);
  Write1D("cm_linear_smoothing", smoothed, spectrum_length);

  MinimumPhaseAnalysis minimum_phase;
  InitializeMinimumPhaseAnalysis(fft_size, &minimum_phase);
  for (int i = 0; i <= fft_size / 2; ++i)
    minimum_phase.log_spectrum[i] = log(power_spectrum[i]);
  GetMinimumPhaseSpectrum(&minimum_phase);
  WriteComplex("cm_minimum_phase", minimum_phase.minimum_phase_spectrum,
      fft_size / 2 + 1);
  DestroyMinimumPhaseAnalysis(&minimum_phase);

  delete[] smoothed;
  delete[] dc_output;
  delete[] power_spectrum;
}

void DumpOptionDefaults(int fs) {
  DioOption dio_option;
  InitializeDioOption(&dio_option);
  double dio_values[6];
  dio_values[0] = dio_option.f0_floor;
  dio_values[1] = dio_option.f0_ceil;
  dio_values[2] = dio_option.channels_in_octave;
  dio_values[3] = dio_option.frame_period;
  dio_values[4] = (double)dio_option.speed;
  dio_values[5] = dio_option.allowed_range;
  Write1D("opt_dio", dio_values, 6);

  HarvestOption harvest_option;
  InitializeHarvestOption(&harvest_option);
  double harvest_values[3];
  harvest_values[0] = harvest_option.f0_floor;
  harvest_values[1] = harvest_option.f0_ceil;
  harvest_values[2] = harvest_option.frame_period;
  Write1D("opt_harvest", harvest_values, 3);

  D4COption d4c_option;
  InitializeD4COption(&d4c_option);
  WriteScalar("opt_d4c", d4c_option.threshold);

  CheapTrickOption cheaptrick_option;
  InitializeCheapTrickOption(fs, &cheaptrick_option);
  double cheaptrick_values[3];
  cheaptrick_values[0] = cheaptrick_option.q1;
  cheaptrick_values[1] = cheaptrick_option.f0_floor;
  cheaptrick_values[2] = (double)cheaptrick_option.fft_size;
  Write1D("opt_cheaptrick", cheaptrick_values, 3);

  const int rates[] = { 8000, 16000, 22050, 24000, 32000, 44100, 48000, 96000 };
  const int rate_count = sizeof(rates) / sizeof(rates[0]);
  double *fft_sizes = new double[rate_count];
  double *f0_floors = new double[rate_count];
  double *aperiodicities = new double[rate_count];
  for (int i = 0; i < rate_count; ++i) {
    CheapTrickOption option;
    InitializeCheapTrickOption(rates[i], &option);
    fft_sizes[i] = (double)option.fft_size;
    f0_floors[i] = GetF0FloorForCheapTrick(rates[i], option.fft_size);
    aperiodicities[i] = (double)GetNumberOfAperiodicities(rates[i]);
  }
  Write1D("opt_fft_size_by_rate", fft_sizes, rate_count);
  Write1D("opt_f0_floor_by_rate", f0_floors, rate_count);
  Write1D("opt_aperiodicities_by_rate", aperiodicities, rate_count);
  delete[] aperiodicities;
  delete[] f0_floors;
  delete[] fft_sizes;
}

void DumpFrameCounts() {
  const int rates[] = { 8000, 16000, 44100, 48000 };
  const double periods[] = { 1.0, 2.5, 5.0, 10.0 };
  const int lengths[] = { 1000, 12345, 48000, 100000 };
  const int count = 4 * 4 * 4;
  double *dio_counts = new double[count];
  double *harvest_counts = new double[count];
  int index = 0;
  for (int r = 0; r < 4; ++r) {
    for (int p = 0; p < 4; ++p) {
      for (int l = 0; l < 4; ++l) {
        dio_counts[index] =
            (double)GetSamplesForDIO(rates[r], lengths[l], periods[p]);
        harvest_counts[index] =
            (double)GetSamplesForHarvest(rates[r], lengths[l], periods[p]);
        ++index;
      }
    }
  }
  Write1D("opt_samples_for_dio", dio_counts, count);
  Write1D("opt_samples_for_harvest", harvest_counts, count);
  delete[] harvest_counts;
  delete[] dio_counts;
}

void DumpPipeline(const double *x, int x_length, int fs) {
  const double frame_period = 5.0;

  double meta[4];
  meta[0] = (double)fs;
  meta[1] = (double)x_length;
  meta[2] = frame_period;
  Write1D("input_x", x, x_length);

  DioOption dio_option;
  InitializeDioOption(&dio_option);
  dio_option.frame_period = frame_period;
  int f0_length = GetSamplesForDIO(fs, x_length, frame_period);
  double *temporal_positions = new double[f0_length];
  double *f0 = new double[f0_length];
  Dio(x, x_length, fs, &dio_option, temporal_positions, f0);
  Write1D("dio_temporal_positions", temporal_positions, f0_length);
  Write1D("dio_f0", f0, f0_length);

  DioOption dio_option_speed2;
  InitializeDioOption(&dio_option_speed2);
  dio_option_speed2.frame_period = frame_period;
  dio_option_speed2.speed = 2;
  double *temporal_positions_speed2 = new double[f0_length];
  double *f0_speed2 = new double[f0_length];
  Dio(x, x_length, fs, &dio_option_speed2, temporal_positions_speed2, f0_speed2);
  Write1D("dio_f0_speed2", f0_speed2, f0_length);
  delete[] f0_speed2;
  delete[] temporal_positions_speed2;

  double *refined_f0 = new double[f0_length];
  StoneMask(x, x_length, fs, temporal_positions, f0, f0_length, refined_f0);
  Write1D("stonemask_f0", refined_f0, f0_length);

  HarvestOption harvest_option;
  InitializeHarvestOption(&harvest_option);
  harvest_option.frame_period = frame_period;
  int harvest_length = GetSamplesForHarvest(fs, x_length, frame_period);
  double *harvest_positions = new double[harvest_length];
  double *harvest_f0 = new double[harvest_length];
  Harvest(x, x_length, fs, &harvest_option, harvest_positions, harvest_f0);
  Write1D("harvest_temporal_positions", harvest_positions, harvest_length);
  Write1D("harvest_f0", harvest_f0, harvest_length);

  CheapTrickOption cheaptrick_option;
  InitializeCheapTrickOption(fs, &cheaptrick_option);
  int fft_size = cheaptrick_option.fft_size;
  int spectrum_length = fft_size / 2 + 1;
  meta[3] = (double)fft_size;
  Write1D("meta", meta, 4);

  double **spectrogram = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i) spectrogram[i] = new double[spectrum_length];
  CheapTrick(x, x_length, fs, temporal_positions, refined_f0, f0_length,
      &cheaptrick_option, spectrogram);
  WriteRows("cheaptrick_spectrogram", spectrogram, f0_length, spectrum_length);

  D4COption d4c_option;
  InitializeD4COption(&d4c_option);
  double **aperiodicity = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i)
    aperiodicity[i] = new double[spectrum_length];
  D4C(x, x_length, fs, temporal_positions, refined_f0, f0_length, fft_size,
      &d4c_option, aperiodicity);
  WriteRows("d4c_aperiodicity", aperiodicity, f0_length, spectrum_length);

  int y_length =
      (int)((f0_length - 1) * frame_period / 1000.0 * fs) + 1;
  double *y = new double[y_length];
  for (int i = 0; i < y_length; ++i) y[i] = 0.0;
  Synthesis(refined_f0, f0_length, spectrogram, aperiodicity, fft_size,
      frame_period, fs, y_length, y);
  Write1D("synthesis_y", y, y_length);

  const int number_of_dimensions = 40;
  int number_of_aperiodicities = GetNumberOfAperiodicities(fs);
  double **coded_aperiodicity = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i)
    coded_aperiodicity[i] = new double[number_of_aperiodicities];
  CodeAperiodicity(aperiodicity, f0_length, fs, fft_size, coded_aperiodicity);
  WriteRows("codec_coded_aperiodicity", coded_aperiodicity, f0_length,
      number_of_aperiodicities);

  double **decoded_aperiodicity = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i)
    decoded_aperiodicity[i] = new double[spectrum_length];
  DecodeAperiodicity(coded_aperiodicity, f0_length, fs, fft_size,
      decoded_aperiodicity);
  WriteRows("codec_decoded_aperiodicity", decoded_aperiodicity, f0_length,
      spectrum_length);

  double **coded_spectrum = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i)
    coded_spectrum[i] = new double[number_of_dimensions];
  CodeSpectralEnvelope(spectrogram, f0_length, fs, fft_size,
      number_of_dimensions, coded_spectrum);
  WriteRows("codec_coded_spectrum", coded_spectrum, f0_length,
      number_of_dimensions);

  double **decoded_spectrum = new double *[f0_length];
  for (int i = 0; i < f0_length; ++i)
    decoded_spectrum[i] = new double[spectrum_length];
  DecodeSpectralEnvelope(coded_spectrum, f0_length, fs, fft_size,
      number_of_dimensions, decoded_spectrum);
  WriteRows("codec_decoded_spectrum", decoded_spectrum, f0_length,
      spectrum_length);

  for (int i = 0; i < f0_length; ++i) {
    delete[] decoded_spectrum[i];
    delete[] coded_spectrum[i];
    delete[] decoded_aperiodicity[i];
    delete[] coded_aperiodicity[i];
    delete[] aperiodicity[i];
    delete[] spectrogram[i];
  }
  delete[] decoded_spectrum;
  delete[] coded_spectrum;
  delete[] decoded_aperiodicity;
  delete[] coded_aperiodicity;
  delete[] aperiodicity;
  delete[] spectrogram;
  delete[] y;
  delete[] harvest_f0;
  delete[] harvest_positions;
  delete[] refined_f0;
  delete[] f0;
  delete[] temporal_positions;
}

}  // namespace

int main(int argc, char **argv) {
  if (argc < 3) {
    printf("usage: worldref <input.wav> <output-directory>\n");
    return 1;
  }
  snprintf(g_outdir, sizeof(g_outdir), "%s", argv[2]);

  int x_length = GetAudioLength(argv[1]);
  if (x_length <= 0) {
    printf("cannot read %s\n", argv[1]);
    return 1;
  }
  double *x = new double[x_length];
  int fs, nbit;
  wavread(argv[1], &fs, &nbit, x);

  DumpFft();
  DumpMatlabRound();
  DumpDecimate();
  DumpInterpolation();
  DumpRandn();
  DumpFastFftFilt();
  DumpSuitableFftSize();
  DumpNuttallWindow();
  DumpSpectrumHelpers();
  DumpOptionDefaults(fs);
  DumpFrameCounts();
  DumpPipeline(x, x_length, fs);

  printf("reference data written to %s\n", g_outdir);
  delete[] x;
  return 0;
}
